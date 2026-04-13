# LuxeVoyage - AI.API (Toxic Comment Detection Service)
#
# This service sits between the review submission flow and Hotel.API.
# When a guest submits a review, Hotel.API publishes an IReviewSubmittedEvent to RabbitMQ
# via MassTransit. MassTransit uses fanout exchanges named after the full interface type,
# so we declare that exchange here and bind our consumer queue to it.
# We publish results back using the same MassTransit exchange convention so Hotel.API's
# ReviewApprovedConsumer / ReviewRejectedConsumer can pick them up normally.

import asyncio
import json
import logging
import os
import pickle
import re
import ssl
import uuid
from datetime import datetime, timezone

import aio_pika
import tensorflow as tf
from dotenv import load_dotenv
from fastapi import FastAPI
from pydantic import BaseModel

# ── Logging ───────────────────────────────────────────────────────────────────
logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger(__name__)

# ── Load .env ─────────────────────────────────────────────────────────────────
load_dotenv()

RABBITMQ_HOST      = os.getenv("RABBITMQ_HOST", "")
RABBITMQ_VHOST     = os.getenv("RABBITMQ_VHOST", "/")
RABBITMQ_USERNAME  = os.getenv("RABBITMQ_USERNAME", "guest")
RABBITMQ_PASSWORD  = os.getenv("RABBITMQ_PASSWORD", "guest")
TOXICITY_THRESHOLD = float(os.getenv("TOXICITY_THRESHOLD", "0.5"))

# MassTransit names exchanges after the full namespace:ClassName of the interface.
# These must match exactly what's in the Shared project including the namespace.
SUBMIT_EXCHANGE   = "Shared.Events:IReviewSubmittedEvent"
APPROVED_EXCHANGE = "Shared.Events:IReviewApprovedEvent"
REJECTED_EXCHANGE = "Shared.Events:IReviewRejectedEvent"

# The queue AI.API consumes from - we create it and bind it to the submit exchange
SUBMIT_QUEUE = "luxevoyage.ai-review-submitted"

# ── Load model artifacts once at startup ──────────────────────────────────────
# Use the directory of this file so paths work regardless of where uvicorn is run from
BASE_DIR = os.path.dirname(os.path.abspath(__file__))

logger.info("Loading TF-IDF vectorizer...")
with open(os.path.join(BASE_DIR, "tfidf_vectorizer.pkl"), "rb") as f:
    vectorizer = pickle.load(f)

logger.info("Loading Keras toxicity model...")
model = tf.keras.models.load_model(os.path.join(BASE_DIR, "toxic_comment_model.h5"))
logger.info("Model loaded - ready to classify comments.")

# ── FastAPI app ───────────────────────────────────────────────────────────────
app = FastAPI(title="LuxeVoyage AI.API", description="Toxic comment detection service")


# ── Text preprocessing ────────────────────────────────────────────────────────
# Must match exactly what was used during training - same contractions, same regex
CONTRACTIONS = {
    "won't": "will not", "can't": "cannot", "n't": " not",
    "'re": " are", "'s": " is", "'d": " would",
    "'ll": " will", "'ve": " have", "'m": " am"
}

def clean_text(text: str) -> str:
    text = text.lower()
    for contraction, expansion in CONTRACTIONS.items():
        text = text.replace(contraction, expansion)
    text = re.sub(r"[^a-z\s]", "", text)
    text = re.sub(r"\s+", " ", text).strip()
    return text


def predict_toxicity(texts: list[str]) -> list[dict]:
    # clean -> TF-IDF transform -> model predict -> threshold decision
    cleaned  = [clean_text(t) for t in texts]
    features = vectorizer.transform(cleaned)
    # The model expects a dense array - sparse matrices from sklearn need converting
    if hasattr(features, "toarray"):
        features = features.toarray()
    probs = model.predict(features, verbose=0).flatten()
    return [
        {
            "text":        orig,
            "toxic":       bool(p >= TOXICITY_THRESHOLD),
            "probability": float(round(p, 4))
        }
        for orig, p in zip(texts, probs)
    ]


# ── HTTP endpoint (for testing without RabbitMQ) ──────────────────────────────
class PredictRequest(BaseModel):
    texts: list[str]

@app.post("/predict")
async def predict(request: PredictRequest):
    return {"results": predict_toxicity(request.texts)}

@app.get("/health")
async def health():
    return {"status": "ok", "model": "loaded"}


# ── RabbitMQ helpers ──────────────────────────────────────────────────────────
def build_masstransit_envelope(payload: dict) -> bytes:
    # MassTransit wraps every message in an envelope with messageId, message body, sentTime etc.
    # Hotel.API's consumers expect this exact structure - raw JSON gets silently dropped.
    return json.dumps({
        "messageId":   str(uuid.uuid4()),
        "messageType": [],
        "message":     payload,
        "sentTime":    datetime.now(timezone.utc).isoformat(),
        "headers":     {},
        "host": {
            "machineName":        "ai-api",
            "processName":        "uvicorn",
            "processId":          os.getpid(),
            "assembly":           "ai-api",
            "assemblyVersion":    "1.0.0",
            "frameworkVersion":   "python",
            "massTransitVersion": "python-aio-pika",
            "operatingSystemVersion": "python"
        }
    }).encode()


async def publish_to_exchange(channel: aio_pika.Channel, exchange_name: str, payload: dict):
    # Declare the fanout exchange (MassTransit style) and publish the envelope to it.
    # Hotel.API's consumer queues are already bound to these exchanges by MassTransit,
    # so the message routes automatically.
    exchange = await channel.declare_exchange(
        exchange_name,
        aio_pika.ExchangeType.FANOUT,
        durable=True
    )
    await exchange.publish(
        aio_pika.Message(
            body=build_masstransit_envelope(payload),
            content_type="application/vnd.masstransit+json",
            delivery_mode=aio_pika.DeliveryMode.PERSISTENT,
        ),
        routing_key=""  # fanout ignores routing key
    )


async def process_review(message: aio_pika.IncomingMessage, channel: aio_pika.Channel):
    async with message.process():
        try:
            # MassTransit wraps the real payload inside a "message" key
            raw  = json.loads(message.body.decode())
            data = raw.get("message", raw)  # fall back to raw if no envelope

            logger.info("Processing review %s for hotel %s", data.get("reviewId"), data.get("hotelId"))

            comment = data.get("comment", "")
            result  = predict_toxicity([comment])[0]
            score   = result["probability"]

            if result["toxic"]:
                logger.warning("Review %s flagged as toxic (score: %.4f)", data.get("reviewId"), score)
                await publish_to_exchange(channel, REJECTED_EXCHANGE, {
                    "reviewId":      data.get("reviewId"),
                    "hotelId":       data.get("hotelId"),
                    "userId":        data.get("userId"),
                    "guestEmail":    data.get("guestEmail"),
                    "toxicityScore": score
                })
            else:
                logger.info("Review %s approved (score: %.4f)", data.get("reviewId"), score)
                await publish_to_exchange(channel, APPROVED_EXCHANGE, {
                    "reviewId":      data.get("reviewId"),
                    "hotelId":       data.get("hotelId"),
                    "toxicityScore": score
                })

        except Exception as ex:
            # Swallow and log - don't crash the consumer loop over one bad message
            logger.error("Failed to process review message: %s", ex)


async def start_consumer():
    url = f"amqps://{RABBITMQ_USERNAME}:{RABBITMQ_PASSWORD}@{RABBITMQ_HOST}/{RABBITMQ_VHOST}"
    ssl_context = ssl.create_default_context()

    logger.info("Connecting to RabbitMQ at %s...", RABBITMQ_HOST)
    connection = await aio_pika.connect_robust(url, ssl=True, ssl_context=ssl_context)

    async with connection:
        channel = await connection.channel()
        await channel.set_qos(prefetch_count=10)

        # Declare the MassTransit fanout exchange for IReviewSubmittedEvent
        submit_exchange = await channel.declare_exchange(
            SUBMIT_EXCHANGE,
            aio_pika.ExchangeType.FANOUT,
            durable=True
        )

        # Declare our queue and bind it to the exchange.
        # This is the missing link - without the binding, messages published to the
        # exchange have nowhere to go and are silently dropped.
        queue = await channel.declare_queue(SUBMIT_QUEUE, durable=True)
        await queue.bind(submit_exchange)

        logger.info("Bound queue '%s' to exchange '%s'", SUBMIT_QUEUE, SUBMIT_EXCHANGE)
        logger.info("Listening for review submissions...")

        async with queue.iterator() as queue_iter:
            async for message in queue_iter:
                await process_review(message, channel)


@app.on_event("startup")
async def on_startup():
    # Run the RabbitMQ consumer as a background asyncio task alongside the HTTP server.
    # Both share the same event loop - no threads needed.
    asyncio.create_task(start_consumer())
    logger.info("AI.API started - HTTP server and RabbitMQ consumer are both running.")

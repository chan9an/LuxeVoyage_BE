# AI.API — Toxic Comment Detection Service

FastAPI service that consumes review submissions from RabbitMQ, runs them through the toxicity model, and publishes the result back.

## Setup

```bash
# Create a virtual environment
python -m venv venv
venv\Scripts\activate   # Windows
# source venv/bin/activate  # Mac/Linux

# Install dependencies
pip install -r requirements.txt
```

## Run

```bash
uvicorn app:app --host 0.0.0.0 --port 8000 --reload
```

## Test the HTTP endpoint directly

```bash
curl -X POST http://localhost:8000/predict \
  -H "Content-Type: application/json" \
  -d '{"texts": ["This hotel was absolutely amazing!", "This place is terrible and the staff are idiots"]}'
```

## How it fits in the flow

1. Guest submits review → Hotel.API saves it (IsApproved=false) → publishes `IReviewSubmittedEvent`
2. AI.API consumes the event → runs `clean_text` → TF-IDF → model predict
3. If clean → publishes `IReviewApprovedEvent` → Hotel.API flips `IsApproved=true`
4. If toxic → publishes `IReviewRejectedEvent` → Hotel.API marks `IsRejected=true`

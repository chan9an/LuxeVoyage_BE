FROM python:3.12-slim

WORKDIR /app

# Install dependencies first (separate layer so Docker cache kicks in on rebuilds)
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy app code
COPY app.py .

# Model files are NOT baked into the image — they are downloaded from Azure Blob Storage
# at container startup via the download script, or mounted as a volume.
# See README.md for instructions on uploading models to blob storage.

EXPOSE 8000

CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "8000"]

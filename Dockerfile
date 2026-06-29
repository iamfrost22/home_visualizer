FROM python:3.11-slim

WORKDIR /app

# Install system dependencies for OpenCV and Cairo
RUN apt-get update && apt-get install -y \
    libgl1-mesa-glx \
    libglib2.0-0 \
    libcairo2 \
    libpango-1.0-0 \
    libpangocairo-1.0-0 \
    libgdk-pixbuf2.0-0 \
    libffi-dev \
    shared-mime-info \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy the repository
COPY . /app

# Install the python dependencies
RUN pip install --no-cache-dir -e .[serve]

# Download the weights directly during the Docker build
RUN mkdir -p weights && \
    curl -L -o weights/best.safetensors https://huggingface.co/Yytsi/floorplan-to-3d-walls/resolve/main/best.safetensors && \
    curl -L -o weights/config.yaml https://huggingface.co/Yytsi/floorplan-to-3d-walls/resolve/main/config.yaml

# Expose the port
EXPOSE 7860

# Command to run the FastAPI server on Hugging Face Spaces (port 7860)
CMD ["uvicorn", "server.main:app", "--host", "0.0.0.0", "--port", "7860"]

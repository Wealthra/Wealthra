## Wealthra OCR

This project extracts structured data from receipt images using Groq's vision models and Pydantic schemas.

### 1. Setup

- **Python**: Use Python 3.10+ and a virtual environment.
- **Install dependencies**:

```bash
pip install -r requirements.txt
```

- **Environment variables**:
  - Create a `.env` file in the project root with:

```bash
GROQ_API_KEY=your_groq_api_key_here
```

> **Important**: Do **not** commit `.env` to git. Make sure `.env` is listed in `.gitignore`.

### 2. Project structure (key files)

- `models.py` – Pydantic models (`Receipt`, `ReceiptItem`) describing the structured receipt data.
- `ocr.py` – OCR module exposing `run_ocr(image_path: str) -> Receipt`.
- `main.py` – Orchestrator/CLI entry point.
- `data/` – Folder for input receipt images (e.g. `data/long-receipt.jpg`, `data/receipt.jpeg`).

### 3. Running the OCR

From the project root:

```bash
python main.py
```

This will:

- Use the default image `data/long-receipt.jpg`.
- Call Groq's vision model.
- Print both the raw JSON and the parsed `Receipt` JSON.

#### Override the input image

- **CLI argument** (preferred):

```bash
python main.py receipt.jpeg
```

This uses `data/receipt.jpeg`.

- **Environment variable**:

```bash
RECEIPT_IMAGE=other.jpg python main.py
```

This uses `data/other.jpg`.

In all cases, `main.py` automatically prepends `data/` to the filename.

### 4. How it works (high level)

- `run_ocr` in `ocr.py`:
  - Reads the image file and encodes it as base64.
  - Sends it to Groq (`meta-llama/llama-4-scout-17b-16e-instruct`) with:
    - A text prompt.
    - The image as a `data:image/jpeg;base64,...` URL.
    - A JSON Schema generated from the `Receipt` Pydantic model.
  - Receives structured JSON, validates it into a `Receipt` instance, and returns it.

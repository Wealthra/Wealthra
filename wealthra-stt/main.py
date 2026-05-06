import os
import json
import re
import tempfile
from datetime import datetime

from dotenv import load_dotenv
from fastapi import FastAPI, File, HTTPException, UploadFile
from groq import Groq
from pydantic import BaseModel

# Load the environment variables from the .env file
load_dotenv()

# Initialize the Groq client automatically using the key from .env
client = Groq(api_key=os.environ.get("GROQ_API_KEY"))

# Define model ids (env-overridable for demo tuning)
AUDIO_MODEL = os.environ.get("STT_AUDIO_MODEL", "whisper-large-v3-turbo")
LLM_MODEL = os.environ.get("STT_LLM_MODEL", "openai/gpt-oss-120b")


def process_audio_file(audio_file_path: str, categories: str | None = None) -> dict:
    """Transcribe an audio file and return structured JSON data."""
    if not os.path.isfile(audio_file_path):
        raise FileNotFoundError(f"File not found: {audio_file_path}")

    print(f"Uploading and transcribing '{audio_file_path}'...")

    with open(audio_file_path, "rb") as file:
        transcription = client.audio.transcriptions.create(
            file=(audio_file_path, file.read()),
            model=AUDIO_MODEL,
        )

    transcript_text = transcription.text
    print(f"Transcription complete! Length: {len(transcript_text)} characters.")
    print(f"Handing off to {LLM_MODEL} for structuring...\n")

    prompt = f"""
    You are an expert finance extraction assistant.
    Extract expense lines from this transcript and return ONLY valid JSON in this shape:
    {{
      "expenses": [
        {{
          "description": "string",
          "amount": number,
          "date": "ISO-8601 date-time string or null",
          "category_hint": "string or null",
          "confidence": number between 0 and 1,
          "source": "stt"
        }}
      ]
    }}
    If there are no expenses, return {{"expenses":[]}}.

    EXISTING CATEGORIES: {categories or 'General, Food, Market, Travel, Health, Entertainment, Others'}

    CRITICAL RULES:
    - Use the transcript language for `description` fields (English or Turkish).
    - Do not mix scripts/languages within the same description.
    - Avoid repetition; do not output duplicate expense lines.
    - If transcript mentions an overall total (toplam/total/all in), treat it as a consistency check,
      not a separate expense row.
    - If exactly one expense amount is missing and one clear overall total exists, infer by subtraction.
      Otherwise keep ambiguous amounts out of output.
    - Return ONLY the JSON object.

    RAW TRANSCRIPT:
    {transcript_text}
    """

    response = client.chat.completions.create(
        model=LLM_MODEL,
        messages=[
            {"role": "user", "content": prompt}
        ],
        response_format={"type": "json_object"},
        temperature=0.0,
    )

    extracted_data = json.loads(response.choices[0].message.content)
    return extracted_data


class ExtractedExpense(BaseModel):
    description: str
    amount: float
    date: datetime | None = None
    category_hint: str | None = None
    confidence: float | None = None
    source: str = "stt"


class ExtractExpensesResponse(BaseModel):
    expenses: list[ExtractedExpense]


app = FastAPI(title="Wealthra STT Service")


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/extract-expenses-from-audio", response_model=ExtractExpensesResponse)
async def extract_expenses_from_audio(
    file: UploadFile = File(...),
    categories: str | None = None
) -> ExtractExpensesResponse:
    suffix = os.path.splitext(file.filename or "")[1] or ".wav"
    try:
        with tempfile.NamedTemporaryFile(delete=False, suffix=suffix) as temp_file:
            temp_file.write(await file.read())
            temp_path = temp_file.name

        extracted_data = process_audio_file(temp_path, categories)
        expenses = extracted_data.get("expenses", [])
        if not isinstance(expenses, list):
            raise ValueError("Invalid payload from model.")

        normalized: list[ExtractedExpense] = []
        for expense in expenses:
            if not isinstance(expense, dict):
                continue

            amount = expense.get("amount")
            if amount is None:
                continue

            description = expense.get("description") or "Audio expense"
            source = expense.get("source") or "stt"
            confidence = expense.get("confidence")
            date_value = expense.get("date")
            parsed_date = None
            if isinstance(date_value, str) and date_value:
                cleaned = re.sub(r"Z$", "+00:00", date_value)
                try:
                    parsed_date = datetime.fromisoformat(cleaned)
                except ValueError:
                    parsed_date = None

            normalized.append(
                ExtractedExpense(
                    description=str(description),
                    amount=float(amount),
                    date=parsed_date,
                    category_hint=expense.get("category_hint"),
                    confidence=float(confidence) if confidence is not None else None,
                    source=str(source),
                )
            )

        return ExtractExpensesResponse(expenses=normalized)
    except Exception as ex:
        raise HTTPException(status_code=500, detail=f"STT extraction failed: {ex}") from ex
    finally:
        if "temp_path" in locals() and os.path.exists(temp_path):
            os.remove(temp_path)
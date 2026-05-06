import os
import tempfile
from datetime import datetime

from dotenv import load_dotenv
from fastapi import FastAPI, File, HTTPException, Query, UploadFile
from groq import Groq
from pydantic import BaseModel

# Load the environment variables from the .env file
load_dotenv()

# Initialize the Groq client automatically using the key from .env
client = Groq(api_key=os.environ.get("GROQ_API_KEY"))

# Define model ids (env-overridable for demo tuning)
AUDIO_MODEL = os.environ.get("STT_AUDIO_MODEL", "whisper-large-v3")
STT_LLM_DEFAULT = os.environ.get("STT_LLM_MODEL", "openai/gpt-oss-120b")


class ExtractedExpense(BaseModel):
    description: str
    amount: float
    date: datetime | None = None
    category_hint: str | None = None
    confidence: float | None = None
    source: str = "stt"


class ExtractExpensesResponse(BaseModel):
    expenses: list[ExtractedExpense]


def process_audio_file(
    audio_file_path: str,
    categories: str | None = None,
    llm_model: str | None = None,
) -> ExtractExpensesResponse:
    """Transcribe an audio file and return structured JSON data."""
    if not os.path.isfile(audio_file_path):
        raise FileNotFoundError(f"File not found: {audio_file_path}")

    active_llm_model = (
        llm_model.strip()
        if llm_model and llm_model.strip()
        else STT_LLM_DEFAULT
    )

    print(f"Uploading and transcribing '{audio_file_path}'...")

    with open(audio_file_path, "rb") as file:
        transcription = client.audio.transcriptions.create(
            file=(audio_file_path, file.read()),
            model=AUDIO_MODEL,
        )

    transcript_text = transcription.text
    print(f"Transcription complete! Length: {len(transcript_text)} characters.")
    print(f"Handing off to {active_llm_model} for structuring...\n")

    prompt = f"""
You are an expert finance data extractor.
Extract all expense lines from this transcript.
Existing categories: {categories or 'General, Food, Market, Travel, Health, Entertainment, Others'}

Rules:
1. Translate descriptions into the language of the transcript.
2. If the user mentions an overall total, use it to infer missing item prices, but do not log the "total" as a separate expense.

Transcript:
{transcript_text}
"""

    schema = ExtractExpensesResponse.model_json_schema()

    response = client.chat.completions.create(
        model=active_llm_model,
        messages=[{"role": "user", "content": prompt}],
        temperature=0.0,
        response_format={
            "type": "json_schema",
            "json_schema": {
                "name": "expenses",
                "strict": False,
                "schema": schema,
            },
        },
    )

    content = response.choices[0].message.content
    if not content:
        raise ValueError("Empty response from model.")
    return ExtractExpensesResponse.model_validate_json(content)


app = FastAPI(title="Wealthra STT Service")


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/extract-expenses-from-audio", response_model=ExtractExpensesResponse)
async def extract_expenses_from_audio(
    file: UploadFile = File(...),
    categories: str | None = None,
    enrichment_model: str | None = Query(default=None),
) -> ExtractExpensesResponse:
    suffix = os.path.splitext(file.filename or "")[1] or ".wav"
    try:
        with tempfile.NamedTemporaryFile(delete=False, suffix=suffix) as temp_file:
            temp_file.write(await file.read())
            temp_path = temp_file.name

        return process_audio_file(temp_path, categories, llm_model=enrichment_model)
    except Exception as ex:
        raise HTTPException(status_code=500, detail=f"STT extraction failed: {ex}") from ex
    finally:
        if "temp_path" in locals() and os.path.exists(temp_path):
            os.remove(temp_path)
from pydantic_settings import BaseSettings

class Settings(BaseSettings):
    PROJECT_NAME: str = "Wealthra Copilot"
    DATABASE_URL: str = "postgresql://wealthra_user:PASSWORD@localhost:5432/wealthra_db"
    GROQ_API_KEY: str

    # ── Model tier configuration ──────────────────────────────────────
    # Enrichment Model: Data extraction, intent routing, JSON generation
    MODEL_FAST: str = "openai/gpt-oss-120b"

    # Default Chat Model: User conversations, analysis, narrative
    MODEL_REASONING: str = "llama-3.3-70b-versatile"

    # SQL model tier: data-read agent primary + fallback
    MODEL_SQL_PRIMARY: str = "meta-llama/llama-4-scout-17b-16e-instruct"
    MODEL_SQL_FALLBACK: str = "llama-3.1-8b-instant"

    # ── Groq resiliency / rate-limit controls ───────────────────────────
    GROQ_MIN_INTERVAL_SECONDS: float = 0.8
    GROQ_MAX_RETRIES: int = 4
    GROQ_RETRY_BASE_SECONDS: float = 2.0
    
    class Config:
        case_sensitive = True
        env_file = [".env", "../.env"]
        env_file_encoding = 'utf-8'
        extra = 'ignore'

settings = Settings()

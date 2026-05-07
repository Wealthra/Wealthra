from pydantic_settings import BaseSettings

class Settings(BaseSettings):
    PROJECT_NAME: str = "Wealthra Copilot"
    DATABASE_URL: str = "postgresql://wealthra_user:PASSWORD@localhost:5432/wealthra_db"
    GEMINI_API_KEY: str | None = None
    GROQ_API_KEY: str | None = None

    # ── Model tier configuration ──────────────────────────────────────
    # Enrichment Model: Data extraction, intent routing, JSON generation
    MODEL_FAST: str = "gemini-3-flash-preview"

    # Default Chat Model: User conversations, analysis, narrative
    MODEL_REASONING: str = "gemini-3-flash-preview"

    # SQL model tier: data-read agent primary + fallback
    MODEL_SQL_PRIMARY: str = "gemini-3-flash-preview"
    MODEL_SQL_FALLBACK: str = "gemini-3-flash-preview"

    # ── Groq resiliency / rate-limit controls ───────────────────────────
    GROQ_MIN_INTERVAL_SECONDS: float = 0.8
    GROQ_MAX_RETRIES: int = 4
    GROQ_RETRY_BASE_SECONDS: float = 2.0

    @property
    def LLM_API_KEY(self) -> str:
        key = self.GEMINI_API_KEY or self.GROQ_API_KEY
        if not key:
            raise ValueError("Missing GEMINI_API_KEY (or fallback GROQ_API_KEY)")
        return key
    
    class Config:
        case_sensitive = True
        env_file = [".env", "../.env"]
        env_file_encoding = 'utf-8'
        extra = 'ignore'

settings = Settings()

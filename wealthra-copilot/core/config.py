from pydantic_settings import BaseSettings

class Settings(BaseSettings):
    PROJECT_NAME: str = "Wealthra Copilot"
    DATABASE_URL: str = "postgresql://wealthra_user:PASSWORD@localhost:5432/wealthra_db"
    GROQ_API_KEY: str

    # ── Model tier configuration ──────────────────────────────────────
    # Fast: simple tasks (intent classification, JSON extraction, small talk)
    # Demo default favors stronger extraction/intent quality over cost.
    MODEL_FAST: str = "qwen/qwen3-32b"

    # Reasoning: complex tasks (SQL generation, financial analysis, narrative)
    # Demo default prioritizes highest perceived intelligence for short sessions.
    MODEL_REASONING: str = "groq/compound"
    
    class Config:
        case_sensitive = True
        env_file = [".env", "../.env"]
        env_file_encoding = 'utf-8'
        extra = 'ignore'

settings = Settings()

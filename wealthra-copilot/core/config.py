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
    
    class Config:
        case_sensitive = True
        env_file = [".env", "../.env"]
        env_file_encoding = 'utf-8'
        extra = 'ignore'

settings = Settings()

from pydantic_settings import BaseSettings

class Settings(BaseSettings):
    PROJECT_NAME: str = "Wealthra Copilot"
    DATABASE_URL: str = "postgresql://wealthra_user:PASSWORD@localhost:5432/wealthra_db"
    GROQ_API_KEY: str

    # ── Model tier configuration ──────────────────────────────────────
    # Fast: simple tasks (intent classification, JSON extraction, small talk)
    #   → llama-3.1-8b-instant: 30 RPM, 500K tokens/day
    MODEL_FAST: str = "llama-3.1-8b-instant"

    # Reasoning: complex tasks (SQL generation, financial analysis, narrative)
    #   → llama-4-scout-17b: 30 RPM, 500K tokens/day, 30K tokens/min
    MODEL_REASONING: str = "meta-llama/llama-4-scout-17b-16e-instruct"
    
    class Config:
        case_sensitive = True
        env_file = [".env", "../.env"]
        env_file_encoding = 'utf-8'
        extra = 'ignore'

settings = Settings()

from pydantic_settings import BaseSettings

class Settings(BaseSettings):
    PROJECT_NAME: str = "Wealthra Copilot"
    DATABASE_URL: str = "postgresql://wealthra_user:PASSWORD@localhost:5432/wealthra_db"
    GROQ_API_KEY: str
    
    class Config:
        case_sensitive = True
        env_file = [".env", "../.env"]
        env_file_encoding = 'utf-8'
        extra = 'ignore'

settings = Settings()


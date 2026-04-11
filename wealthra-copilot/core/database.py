from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker
from core.config import settings

# Since we are in docker, 'db' is the hostname
# DATABASE_URL should be something like: postgresql://wealthra_user:password@db:5432/wealthra_db
engine = create_engine(settings.DATABASE_URL)
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)

def get_db():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()

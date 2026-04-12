from pydantic import BaseModel, Field
from typing import List, Dict, Any, Optional

class ExportColumn(BaseModel):
    key: str
    label: str

class ExportRequest(BaseModel):
    title: str = "Wealthra Report"
    columns: List[ExportColumn]
    data: List[Dict[str, Any]]
    filename: Optional[str] = None
    branding: Optional[Dict[str, Any]] = None

class HealthResponse(BaseModel):
    status: str
    service: str
    version: str

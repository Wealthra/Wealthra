import logging
from fastapi import FastAPI, Response, HTTPException
from fastapi.responses import StreamingResponse
from core.schemas import ExportRequest, HealthResponse
from services.export_service import ExportService

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s | %(name)-25s | %(levelname)-5s | %(message)s",
)
logger = logging.getLogger(__name__)

app = FastAPI(
    title="Wealthra Export Service",
    description="Microservice for generating PDF and Excel reports",
    version="1.0.0",
)

@app.get("/health", response_model=HealthResponse)
def health_check():
    """Basic health check endpoint."""
    return {"status": "healthy", "service": "wealthra-export-pdf-excel", "version": "1.0.0"}

@app.post("/export/pdf")
async def export_pdf(request: ExportRequest):
    """
    Generate and return a PDF file based on the provided data.
    """
    try:
        logger.info(f"Generating PDF: {request.title}")
        pdf_buffer = await ExportService.generate_pdf(request)
        
        filename = request.filename or "report.pdf"
        if not filename.endswith(".pdf"):
            filename += ".pdf"
            
        headers = {
            'Content-Disposition': f'attachment; filename="{filename}"'
        }
        return StreamingResponse(pdf_buffer, media_type="application/pdf", headers=headers)
    except Exception as e:
        logger.error(f"Error generating PDF: {str(e)}")
        raise HTTPException(status_code=500, detail=f"Failed to generate PDF: {str(e)}")

@app.post("/export/excel")
async def export_excel(request: ExportRequest):
    """
    Generate and return an Excel file based on the provided data.
    """
    try:
        logger.info(f"Generating Excel: {request.title}")
        excel_buffer = await ExportService.generate_excel(request)
        
        filename = request.filename or "report.xlsx"
        if not filename.endswith(".xlsx"):
            filename += ".xlsx"
            
        headers = {
            'Content-Disposition': f'attachment; filename="{filename}"'
        }
        return StreamingResponse(
            excel_buffer, 
            media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
            headers=headers
        )
    except Exception as e:
        logger.error(f"Error generating Excel: {str(e)}")
        raise HTTPException(status_code=500, detail=f"Failed to generate Excel: {str(e)}")

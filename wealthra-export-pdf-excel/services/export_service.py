import io
import pandas as pd
from reportlab.lib.pagesizes import A4, landscape
from reportlab.lib import colors
from reportlab.platypus import SimpleDocTemplate, Table, TableStyle, Paragraph, Spacer
from reportlab.lib.styles import getSampleStyleSheet
from core.schemas import ExportRequest

class ExportService:
    @staticmethod
    async def generate_pdf(request: ExportRequest) -> io.BytesIO:
        buffer = io.BytesIO()
        doc = SimpleDocTemplate(buffer, pagesize=landscape(A4))
        elements = []
        
        styles = getSampleStyleSheet()
        title_style = styles['Heading1']
        title_style.alignment = 1  # Center
        
        # Add Title
        elements.append(Paragraph(request.title, title_style))
        elements.append(Spacer(1, 20))
        
        # Prepare Table Data
        headers = [col.label for col in request.columns]
        table_data = [headers]
        
        for item in request.data:
            row = [str(item.get(col.key, "")) for col in request.columns]
            table_data.append(row)
            
        # Create Table
        t = Table(table_data, repeatRows=1)
        t.setStyle(TableStyle([
            ('BACKGROUND', (0, 0), (-1, 0), colors.HexColor("#1A237E")),
            ('TEXTCOLOR', (0, 0), (-1, 0), colors.whitesmoke),
            ('ALIGN', (0, 0), (-1, -1), 'CENTER'),
            ('FONTNAME', (0, 0), (-1, 0), 'Helvetica-Bold'),
            ('FONTSIZE', (0, 0), (-1, 0), 12),
            ('BOTTOMPADDING', (0, 0), (-1, 0), 12),
            ('BACKGROUND', (0, 1), (-1, -1), colors.whitesmoke),
            ('GRID', (0, 0), (-1, -1), 1, colors.grey),
            ('VALIGN', (0, 0), (-1, -1), 'MIDDLE'),
        ]))
        
        elements.append(t)
        doc.build(elements)
        buffer.seek(0)
        return buffer

    @staticmethod
    async def generate_excel(request: ExportRequest) -> io.BytesIO:
        buffer = io.BytesIO()
        
        # Map data to a list of dicts with human-readable keys
        mapped_data = []
        for item in request.data:
            mapped_item = {}
            for col in request.columns:
                mapped_item[col.label] = item.get(col.key, "")
            mapped_data.append(mapped_item)
            
        df = pd.DataFrame(mapped_data)
        
        with pd.ExcelWriter(buffer, engine='openpyxl') as writer:
            df.to_excel(writer, index=False, sheet_name='Report')
            
        buffer.seek(0)
        return buffer

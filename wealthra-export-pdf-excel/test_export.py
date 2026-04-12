import json
from fastapi.testclient import TestClient
from main import app

client = TestClient(app)

def test_health():
    response = client.get("/health")
    assert response.status_code == 200
    assert response.json()["status"] == "healthy"

def test_export_pdf():
    payload = {
        "title": "Monthly Financial Report",
        "columns": [
            {"key": "date", "label": "Date"},
            {"key": "description", "label": "Description"},
            {"key": "amount", "label": "Amount ($)"}
        ],
        "data": [
            {"date": "2024-03-01", "description": "Salary", "amount": 5000},
            {"date": "2024-03-05", "description": "Rent", "amount": -1500},
            {"date": "2024-03-10", "description": "Groceries", "amount": -200},
            {"date": "2024-03-15", "description": "Utilities", "amount": -100}
        ],
        "filename": "monthly_report.pdf"
    }
    
    response = client.post("/export/pdf", json=payload)
    assert response.status_code == 200
    assert response.headers["content-type"] == "application/pdf"
    assert "attachment; filename=\"monthly_report.pdf\"" in response.headers["content-disposition"]
    print("PDF export test passed!")

def test_export_excel():
    payload = {
        "title": "Stock Portfolio",
        "columns": [
            {"key": "symbol", "label": "Symbol"},
            {"key": "shares", "label": "Shares"},
            {"key": "price", "label": "Current Price"}
        ],
        "data": [
            {"symbol": "AAPL", "shares": 10, "price": 180.50},
            {"symbol": "GOOGL", "shares": 5, "price": 145.20},
            {"symbol": "MSFT", "shares": 8, "price": 405.10}
        ],
        "filename": "portfolio.xlsx"
    }
    
    response = client.post("/export/excel", json=payload)
    assert response.status_code == 200
    assert response.headers["content-type"] == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    assert "attachment; filename=\"portfolio.xlsx\"" in response.headers["content-disposition"]
    print("Excel export test passed!")

if __name__ == "__main__":
    print("Running tests...")
    test_health()
    test_export_pdf()
    test_export_excel()
    print("All tests passed!")

import pathlib
import sys

from fastapi.testclient import TestClient

sys.path.append(str(pathlib.Path(__file__).resolve().parents[1]))
import main
from models import Receipt, ReceiptItem


def test_health() -> None:
    client = TestClient(main.app)
    response = client.get("/health")
    assert response.status_code == 200
    assert response.json()["status"] == "ok"


def test_extract_expenses_from_image(monkeypatch) -> None:
    def fake_run_ocr(_: str) -> Receipt:
        return Receipt(
            merchant_name="Store",
            date="2026-01-01",
            time="10:00",
            total_amount=25.0,
            tax_amount=2.0,
            items=[ReceiptItem(item_name="Milk", price=5.5)],
        )

    monkeypatch.setattr(main, "run_ocr", fake_run_ocr)

    client = TestClient(main.app)
    response = client.post(
        "/extract-expenses-from-image",
        files={"file": ("receipt.jpg", b"fake-image", "image/jpeg")},
    )

    assert response.status_code == 200
    payload = response.json()
    assert len(payload["expenses"]) == 1
    assert payload["expenses"][0]["description"] == "Milk"

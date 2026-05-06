import os
import tempfile
from datetime import datetime

from fastapi import FastAPI, File, HTTPException, UploadFile
from pydantic import BaseModel

from ocr import run_ocr


class ExtractedExpense(BaseModel):
    description: str
    amount: float
    currency: str = "TRY"
    date: datetime | None = None
    category_hint: str | None = None
    confidence: float | None = None
    source: str = "ocr"


class ExtractExpensesResponse(BaseModel):
    expenses: list[ExtractedExpense]


app = FastAPI(title="Wealthra OCR Service")


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/extract-expenses-from-image", response_model=ExtractExpensesResponse)
async def extract_expenses_from_image(
    file: UploadFile = File(...),
    categories: str | None = None
) -> ExtractExpensesResponse:
    suffix = os.path.splitext(file.filename or "")[1] or ".jpg"

    try:
        with tempfile.NamedTemporaryFile(delete=False, suffix=suffix) as temp_file:
            temp_file.write(await file.read())
            temp_path = temp_file.name

        receipt = run_ocr(temp_path, categories)
        parsed_date = None
        if receipt.date:
            try:
                parsed_date = datetime.fromisoformat(receipt.date)
            except ValueError:
                parsed_date = None

        receipt_currency = (receipt.currency or "").strip().upper()
        if not receipt_currency:
            receipt_currency = "TRY"

        expenses: list[ExtractedExpense] = []
        items_sum = 0.0
        for item in receipt.items or []:
            if item.price is None:
                continue
            price = float(item.price)
            items_sum += price
            expenses.append(
                ExtractedExpense(
                    description=item.item_name or "Unknown item",
                    amount=price,
                    currency=receipt_currency,
                    date=parsed_date,
                    category_hint="receipt",
                    confidence=0.8,
                )
            )

        if receipt.total_amount is not None:
            final_total = float(receipt.total_amount)
            missing_money = round(final_total - items_sum, 2)
            if missing_money > 0.05:
                merchant = receipt.merchant_name or "Receipt"
                expenses.append(
                    ExtractedExpense(
                        description=f"Tax & Tip ({merchant})",
                        amount=missing_money,
                        currency=receipt_currency,
                        date=parsed_date,
                        category_hint="Fees & Taxes",
                        confidence=0.9,
                    )
                )

        return ExtractExpensesResponse(expenses=expenses)
    except Exception as ex:
        raise HTTPException(status_code=500, detail=f"OCR extraction failed: {ex}") from ex
    finally:
        if "temp_path" in locals() and os.path.exists(temp_path):
            os.remove(temp_path)

from pydantic import BaseModel, Field

class ReceiptItem(BaseModel):
    item_name: str | None = Field(
        description="Name of the product as written on the receipt."
    )
    price: float | None = Field(
        description=(
            "The line amount for this item exactly as printed next to that "
            "product. Do NOT include receipt-level tax, tip, or service "
            "charges here if they appear only below a subtotal."
        )
    )


class Receipt(BaseModel):
    merchant_name: str | None = Field(
        description="Merchant or store name printed on the receipt."
    )
    date: str | None = Field(
        description="Date of the transaction, as printed on the receipt."
    )
    time: str | None = Field(
        description="Time of the transaction, as printed on the receipt."
    )
    currency: str | None = Field(
        description=(
            "ISO 4217 currency code for this receipt (e.g. USD, TRY, EUR). "
            "Infer from symbols such as $, ₺, €, £ or explicit text."
        )
    )
    total_amount: float | None = Field(
        description=(
            "The absolute final amount charged/paid on the receipt, including "
            "all taxes, tips, and fees — the number that should match the "
            "card or cash total."
        )
    )
    tax_amount: float | None = Field(
        description=(
            "Total tax amount for the whole receipt, if present. This should "
            "cover all consumption or sales taxes that apply to the receipt "
            "as a whole (other tax types). Do "
            "NOT specialize this field to a single tax type; treat it as the "
            "sum of all tax totals printed near the bottom of the receipt."
        )
    )
    items: list[ReceiptItem] | None = Field(
        description=(
            "Physical products and priced dish/drink lines only. Do NOT add "
            "separate rows for tax, tip, gratuity, or service charge if they "
            "only appear after a subtotal; those are captured via total_amount "
            "minus the sum of these items."
        )
    )
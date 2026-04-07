from pydantic import BaseModel, Field

class ReceiptItem(BaseModel):
    item_name: str | None = Field(
        description="Name of the product as written on the receipt."
    )
    price: float | None = Field(
        description=(
            "The full line amount for this item INCLUDING any taxes, "
            "exactly as printed on the receipt. Do NOT subtract tax from "
            "this value even if the receipt also lists a separate tax_amount."
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
    total_amount: float | None = Field(
        description=(
            "Total amount paid on the receipt INCLUDING all taxes and fees, "
            "as printed near the bottom."
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
            "List of line items on the receipt. Each item's price must match "
            "the line total on the receipt and must not be adjusted by the "
            "receipt-level tax_amount."
        )
    )
from dotenv import load_dotenv
from groq import Groq
from models import Receipt
import base64
import os

load_dotenv()


client = Groq(api_key=os.getenv("GROQ_API_KEY"))
MODEL_ID = os.getenv("OCR_MODEL", "meta-llama/llama-4-scout-17b-16e-instruct")


def run_ocr(image_path: str, categories: str | None = None) -> Receipt:
    print(f"Loading receipt image from {image_path}...")

    with open(image_path, "rb") as f:
        image_bytes = f.read()

    image_b64 = base64.b64encode(image_bytes).decode("utf-8")

    prompt = (
        "Extract structured data from this shopping receipt and return "
        "ONLY JSON matching the provided schema.\n\n"
        "- Set `currency` to the ISO 4217 code (USD, TRY, EUR, GBP, …) using "
        "symbols or text on the receipt ($, ₺, €, £, TL, etc.).\n"
        "- `total_amount` is the FINAL amount charged (after tax and tip), "
        "the figure that should match the customer's card or cash payment.\n"
        "- `items` lists priced product/dish lines only. Do NOT put tax, tip, "
        "gratuity, or service charge in `items` when they appear only below a "
        "subtotal; leave them out so totals can be reconciled.\n"
        "- For each item, `price` is the amount printed on that line only.\n"
        "- The `tax_amount` field is the total tax on the receipt if shown; "
        "do not subtract it from line items.\n"
        "- Each distinct printed line item MUST appear at most once in `items`. "
        "Do not duplicate the same product with the same price.\n"
        "- If text is unreadable, leave uncertain optional fields null/empty per schema; "
        "do not hallucinate product names or prices.\n"
        "- Treat words like total/toplam/genel toplam as receipt-level totals, not item lines.\n"
        f"- Use these categories for matching if possible: {categories or 'General, Food, Market, Travel, Health, Entertainment, Others'}\n"
        "- Respond in the language of the receipt for extracted text values, but keep schema field names unchanged.\n"
        "- Do not mix English and Turkish within the same extracted text field unless the receipt itself does.\n"
        "- Kendini tekrar etme (avoid redundancy), her bilgiyi bir kez ve öz söyle.\n"
    )

    print("Analyzing image and extracting structured data with Groq...\n")

    schema = Receipt.model_json_schema()

    completion = client.chat.completions.create(
        model=MODEL_ID,
        messages=[
            {
                "role": "system",
                "content": "You are an OCR and information extraction engine that outputs strictly valid JSON.",
            },
            {
                "role": "user",
                "content": [
                    {
                        "type": "text",
                        "text": prompt,
                    },
                    {
                        "type": "image_url",
                        "image_url": {
                            "url": f"data:image/jpeg;base64,{image_b64}",
                        },
                    },
                ],
            },
        ],
        temperature=0.0,
        response_format={
            "type": "json_schema",
            "json_schema": {
                "name": "receipt",
                "strict": False,  # best-effort to match the schema
                "schema": schema,
            },
        },
    )

    parsed = Receipt.model_validate_json(completion.choices[0].message.content)

    return parsed
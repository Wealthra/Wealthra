from dotenv import load_dotenv
from groq import Groq
from models import Receipt
import base64
import os

load_dotenv()


client = Groq(api_key=os.getenv("GROQ_API_KEY"))
MODEL_ID = "meta-llama/llama-4-scout-17b-16e-instruct"


def run_ocr(image_path: str, categories: str | None = None) -> Receipt:
    print(f"Loading receipt image from {image_path}...")

    with open(image_path, "rb") as f:
        image_bytes = f.read()

    image_b64 = base64.b64encode(image_bytes).decode("utf-8")

    prompt = (
        "Extract structured data from this shopping receipt and return "
        "ONLY JSON matching the provided schema.\n\n"
        "- For each item, the `price` MUST be the full line amount EXACTLY as "
        "printed on the receipt (including any taxes), without subtracting "
        "any tax.\n"
        "- The `tax_amount` field is for the TOTAL TAX on the whole receipt. "
        "This should combine all tax types shown on the receipt (for example "
        "named taxes) into a single numeric value. It "
        "MUST NOT be subtracted from or distributed into item prices.\n"
        "- Ensure that the sum of all item prices is consistent with the "
        "`total_amount` on the receipt, within minor rounding differences.\n"
        "- Each distinct printed line item MUST appear at most once in `items`. "
        "Do not duplicate the same product with the same price.\n"
        f"- Use these categories for matching if possible: {categories or 'General, Food, Market, Travel, Health, Entertainment, Others'}\n"
        "- Respond in the language of the receipt, but keep field names as per schema.\n"
        "- Sadece ve sadece hedef dilde yanıt ver, araya başka dillerden karakter/kelime karıştırma.\n"
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
"""
Language detection for Turkish / English.

Uses a fast heuristic approach based on Turkish-specific characters
and common keywords. Falls back to LLM detection only when the
heuristic score is inconclusive (ambiguous range).
"""

import re
from typing import Optional


# Turkish-specific Unicode characters
_TR_CHARS = set("çÇğĞıİöÖşŞüÜ")

# High-frequency Turkish words (stopwords / common verbs)
_TR_KEYWORDS = {
    "bir", "ve", "bu", "için", "ile", "çok", "var", "yok",
    "ne", "nasıl", "kadar", "bana", "benim", "evet", "hayır",
    "tamam", "lütfen", "teşekkür", "merhaba", "selam",
    "harcadım", "harcama", "gelir", "gider", "maaş", "fatura",
    "toplam", "kategori", "lira", "kuruş", "ay", "yıl", "hafta",
    "bugün", "dün", "geçen", "listele", "göster", "kaydet",
    "ekle", "sil", "güncelle", "analiz", "rapor", "bütçe",
    "para", "market", "kira", "elektrik", "su", "doğalgaz",
    "ulaşım", "yemek", "sağlık", "eğitim", "eğlence",
    "onayla", "onaylıyorum", "iptal"
}

# High-frequency English words for counter-scoring
_EN_KEYWORDS = {
    "the", "is", "are", "was", "were", "have", "has", "had",
    "what", "how", "much", "many", "please", "thanks", "yes", "no",
    "spent", "spend", "income", "expense", "salary", "bill",
    "total", "category", "month", "year", "week", "today",
    "yesterday", "last", "list", "show", "save", "add", "delete",
    "update", "analyze", "report", "budget", "money", "rent",
    "confirm", "cancel", "okay"
}


class LanguageDetector:
    """Detects whether user input is Turkish or English."""

    def detect(self, text: str) -> str:
        """
        Returns 'tr' for Turkish, 'en' for English.

        Scoring algorithm:
          1. Turkish character presence → strong TR signal
          2. Keyword matching against TR/EN dictionaries
          3. If score is ambiguous, default to English
        """
        if not text or not text.strip():
            return "en"

        score = 0.0
        lower = text.lower()
        words = set(re.findall(r'\b\w+\b', lower))

        # Phase 1: Turkish character detection (strong signal)
        tr_char_count = sum(1 for ch in text if ch in _TR_CHARS)
        if tr_char_count > 0:
            score += min(tr_char_count * 15, 60)  # Cap at 60

        # Phase 2: Keyword matching
        tr_hits = len(words & _TR_KEYWORDS)
        en_hits = len(words & _EN_KEYWORDS)

        score += tr_hits * 10
        score -= en_hits * 10

        # Threshold: positive → Turkish, negative/zero → English
        return "tr" if score > 0 else "en"

"""
Language detection for Turkish / English.

Primary: Lingua n-gram model restricted to EN/TR (accurate on short chat,
ASCII Turkish, and mixed phrasing). Fallback: Turkish orthography + token hints
when Lingua is unavailable or yields no confident language.
"""

from __future__ import annotations

import re
import unicodedata
from typing import Optional

# Turkish-specific Unicode characters
_TR_CHARS = frozenset("çÇğĞıİöÖşŞüÜ")

# High-frequency Turkish tokens (incl. financial / copilot vocabulary)
_TR_HINTS = frozenset(
    {
        "bir", "ve", "bu", "için", "ile", "çok", "var", "yok",
        "ne", "nasıl", "kadar", "bana", "benim", "evet", "hayır",
        "tamam", "lütfen", "teşekkür", "merhaba", "selam",
        "harcadım", "harcama", "gelir", "gider", "maaş", "fatura",
        "toplam", "kategori", "lira", "kuruş", "ay", "yıl", "hafta",
        "bugün", "dün", "geçen", "listele", "göster", "kaydet",
        "ekle", "sil", "güncelle", "analiz", "rapor", "bütçe",
        "para", "market", "kira", "elektrik", "su", "doğalgaz",
        "ulaşım", "yemek", "sağlık", "eğitim", "eğlence",
        "onayla", "onaylıyorum", "iptal", "masraf", "nedir", "kaç",
        "liradan", "harcamam", "harcamalarımı",
        # ASCII-only typing / informal
        "tesekkur", "tesekkurler", "goster", "butce", "masraf",
    }
)

_EN_HINTS = frozenset(
    {
        "the", "a", "an", "is", "are", "was", "were", "have", "has", "had",
        "what", "how", "which", "when", "where", "why", "who",
        "much", "many", "please", "thanks", "thank", "yes", "no",
        "spent", "spend", "income", "expense", "salary", "bill",
        "total", "category", "month", "year", "week", "today",
        "yesterday", "last", "list", "show", "save", "add", "delete",
        "update", "analyze", "analyse", "report", "budget", "money",
        "rent", "confirm", "cancel", "okay", "ok", "hi", "hey", "hello",
        "pls", "thx", "could", "would", "should", "my", "me", "i",
    }
)

_TOKEN_RE = re.compile(r"(?u)\b\w+\b")

try:
    from lingua import Language, LanguageDetectorBuilder

    _LINGUA_TR = Language.TURKISH
    _LINGUA_EN = Language.ENGLISH
    _lingua_builder = (
        LanguageDetectorBuilder.from_languages(Language.ENGLISH, Language.TURKISH)
        .with_preloaded_language_models()
    )
    _LINGUA_AVAILABLE = True
except ImportError:
    _LINGUA_TR = None  # type: ignore[misc, assignment]
    _LINGUA_EN = None  # type: ignore[misc, assignment]
    _lingua_builder = None
    _LINGUA_AVAILABLE = False


def _normalize(text: str) -> str:
    return unicodedata.normalize("NFC", (text or "").strip())


def _has_letter(text: str) -> bool:
    return any(ch.isalpha() for ch in text)


def _fallback_lang(text: str) -> str:
    """
    Heuristic fallback: Turkish letters strongly indicate TR; otherwise
    score bilingual hint tokens (short utterances, Lingua unavailable).
    """
    if not text or not text.strip():
        return "en"

    score = 0.0
    lower = text.lower()
    words = set(_TOKEN_RE.findall(lower))

    tr_char_count = sum(1 for ch in text if ch in _TR_CHARS)
    if tr_char_count:
        score += min(tr_char_count * 15, 60)

    score += len(words & _TR_HINTS) * 10
    score -= len(words & _EN_HINTS) * 10

    return "tr" if score > 0 else "en"


class LanguageDetector:
    """Detects whether user input is Turkish or English ('tr' / 'en')."""

    def __init__(self) -> None:
        self._lingua: Optional[object] = None
        if _LINGUA_AVAILABLE:
            self._lingua = _lingua_builder.build()

    def detect(self, text: str) -> str:
        normalized = _normalize(text)
        if not normalized:
            return "en"
        if not _has_letter(normalized):
            return "en"

        if self._lingua is not None:
            result = self._lingua.detect_language_of(normalized)
            if result == _LINGUA_TR:
                return "tr"
            if result == _LINGUA_EN:
                return "en"

        return _fallback_lang(normalized)

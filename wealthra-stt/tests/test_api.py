import pathlib
import sys

from fastapi.testclient import TestClient

sys.path.append(str(pathlib.Path(__file__).resolve().parents[1]))
import main


def test_health() -> None:
    client = TestClient(main.app)
    response = client.get("/health")
    assert response.status_code == 200
    assert response.json()["status"] == "ok"


def test_extract_expenses_from_audio(monkeypatch) -> None:
    def fake_process_audio_file(_: str) -> dict:
        return {
            "expenses": [
                {
                    "description": "Coffee",
                    "amount": 3.4,
                    "date": "2026-01-02T12:00:00",
                    "category_hint": "food",
                    "confidence": 0.9,
                    "source": "stt",
                }
            ]
        }

    monkeypatch.setattr(main, "process_audio_file", fake_process_audio_file)

    client = TestClient(main.app)
    response = client.post(
        "/extract-expenses-from-audio",
        files={"file": ("note.wav", b"fake-audio", "audio/wav")},
    )

    assert response.status_code == 200
    payload = response.json()
    assert len(payload["expenses"]) == 1
    assert payload["expenses"][0]["description"] == "Coffee"

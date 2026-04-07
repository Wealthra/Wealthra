## Wealthra STT

This project transcribes audio files and extracts structured information (language, summary, action items, key entities) using Groq's Whisper and Llama models.

### 1. Setup

- **Python**: Use Python 3.10+ and a virtual environment.
- **Install dependencies**:

```bash
pip install -r requirements.txt
```

- **Environment variables**:
  - Create a `.env` file in the project root with:

```bash
GROQ_API_KEY=your_groq_api_key_here
```

> **Important**: Do **not** commit `.env` to git. Make sure `.env` is listed in `.gitignore`.

### 2. Project structure (key files)

- `main.py` – Core transcription + LLM pipeline and CLI entry point.
- `data/` – Folder for input audio files (e.g. `data/meeting.m4a`, `data/notes.wav`).
- `requirements.txt` – Python dependencies (`groq`, `python-dotenv`).

### 3. Running the transcriber

From the project root, pass the audio filename that lives under `data/`:

```bash
python main.py your-audio-file.m4a
```

This will:

- Read `data/your-audio-file.m4a`.
- Send it to Groq Whisper (`whisper-large-v3-turbo`) for transcription.
- Send the transcript to Llama (`llama-3.1-8b-instant`) to extract structured JSON.
- Print the extracted JSON to stdout.

### 4. How it works (high level)

- `main.py` loads `GROQ_API_KEY` from `.env` and initializes the Groq client.
- `process_audio_file(audio_file_path: str)`:
  - Opens the audio file and sends it to Groq's Whisper endpoint to get a transcript.
  - Builds a prompt instructing Llama to:
    - Detect language (`English` or `Turkish`).
    - Summarize the content.
    - Extract `action_items` and `key_entities`.
  - Calls the chat completion API in JSON mode and parses the returned JSON.
- The CLI wrapper:
  - Takes the audio filename as an argument.
  - Prepends `data/` to build the full path.
  - Calls `process_audio_file` and prints the resulting JSON.

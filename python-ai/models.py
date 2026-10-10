import os
from functools import lru_cache
from pathlib import Path

from dotenv import load_dotenv
from langchain_groq import ChatGroq

load_dotenv(Path(__file__).with_name(".env"), override=False)

# Preserve the model naming pattern used by the example agent code.
@lru_cache(maxsize=1)
def get_open_ai_model() -> ChatGroq:
    api_key = os.getenv("GROQ_API_KEY")
    if not api_key:
        raise RuntimeError("GROQ_API_KEY is not configured. Copy .env.example to .env and set the key.")

    langsmith_key = os.getenv("LANGSMITH_API_KEY")
    if langsmith_key:
        os.environ["LANGCHAIN_PROJECT"] = os.getenv("LANGCHAIN_PROJECT", "CRR Risk Summary")


    return ChatGroq(
        model=os.getenv("GROQ_MODEL", "llama-3.3-70b-versatile"),
        api_key=api_key,
        temperature=0.2,
    )

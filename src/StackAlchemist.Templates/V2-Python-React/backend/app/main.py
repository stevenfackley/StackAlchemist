from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from app.config import settings
from app.database import engine, Base
{{#each Entities}}
from app.routers import {{NameLower}} as {{NameLower}}_router
{{/each}}


@asynccontextmanager
async def lifespan(_app: FastAPI):
    # Tables are created when the server starts, not when this module is imported:
    # importing app.main (pytest collection, tooling) must never need a database.
    Base.metadata.create_all(bind=engine)
    yield


app = FastAPI(
    title="{{ProjectName}} API",
    version="1.0.0",
    lifespan=lifespan,
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=[settings.frontend_url],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

{{#each Entities}}
app.include_router({{NameLower}}_router.router)
{{/each}}


@app.get("/healthz")
def healthz():
    return {"status": "ok", "service": "{{ProjectNameKebab}}-api"}

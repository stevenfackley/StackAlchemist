from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from app.config import settings
from app.database import engine, Base

app = FastAPI(
    title="{{ProjectName}} API",
    version="1.0.0",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=[settings.frontend_url],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

Base.metadata.create_all(bind=engine)

[[LLM_INJECTION_START: RouteRegistrations]]
# LLM generates:
# from app.routers import entity_router
# app.include_router(entity_router.router, prefix="/api/v1", tags=["entities"])
[[LLM_INJECTION_END: RouteRegistrations]]


@app.get("/healthz")
def healthz():
    return {"status": "ok", "service": "{{ProjectNameKebab}}-api"}

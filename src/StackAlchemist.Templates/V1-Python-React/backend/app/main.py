from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from app.config import settings


# The schema comes from the Alembic migrations (`alembic upgrade head`), which the container
# runs before it starts the server. Importing app.main never needs a database.
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

[[LLM_INJECTION_START: RouteRegistrations]]
# LLM generates:
# from app.routers import entity_router
# app.include_router(entity_router.router, prefix="/api/v1", tags=["entities"])
[[LLM_INJECTION_END: RouteRegistrations]]


@app.get("/healthz")
def healthz():
    return {"status": "ok", "service": "{{ProjectNameKebab}}-api"}

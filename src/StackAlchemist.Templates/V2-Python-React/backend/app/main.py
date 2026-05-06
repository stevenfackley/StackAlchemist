from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from app.config import settings
from app.database import engine, Base
{{#each Entities}}
from app.routers import {{NameLower}} as {{NameLower}}_router
{{/each}}

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

{{#each Entities}}
app.include_router({{NameLower}}_router.router)
{{/each}}


@app.get("/healthz")
def healthz():
    return {"status": "ok", "service": "{{ProjectNameKebab}}-api"}

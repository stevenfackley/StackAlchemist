from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from app.config import settings
{{#each Entities}}
from app.routers import {{NameLower}} as {{NameLower}}_router
{{/each}}


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

{{#each Entities}}
app.include_router({{NameLower}}_router.router)
{{/each}}


@app.get("/healthz")
def healthz():
    return {"status": "ok", "service": "{{ProjectNameKebab}}-api"}

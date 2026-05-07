from typing import Sequence
from uuid import UUID
from sqlalchemy.orm import Session
from app.models.{{EntityNameLower}} import {{EntityName}}
from app.schemas.{{EntityNameLower}} import Create{{EntityName}}


def get_all(db: Session) -> Sequence[{{EntityName}}]:
    [[LLM_INJECTION_START: GetAllImpl]]
    return db.query({{EntityName}}).order_by({{EntityName}}.created_at.desc()).all()
    [[LLM_INJECTION_END: GetAllImpl]]


def get_by_id(db: Session, id: UUID) -> {{EntityName}} | None:
    [[LLM_INJECTION_START: GetByIdImpl]]
    return db.query({{EntityName}}).filter({{EntityName}}.id == id).first()
    [[LLM_INJECTION_END: GetByIdImpl]]


def create(db: Session, payload: Create{{EntityName}}) -> {{EntityName}}:
    [[LLM_INJECTION_START: CreateImpl]]
    # LLM fills: build a {{EntityName}} from payload.model_dump(), add+commit+refresh, return.
    raise NotImplementedError("Zone CreateImpl not yet generated.")
    [[LLM_INJECTION_END: CreateImpl]]


def update(db: Session, id: UUID, payload: Create{{EntityName}}) -> {{EntityName}} | None:
    [[LLM_INJECTION_START: UpdateImpl]]
    # LLM fills: load by id, apply payload.model_dump() field-by-field, commit, return updated entity (or None if not found).
    raise NotImplementedError("Zone UpdateImpl not yet generated.")
    [[LLM_INJECTION_END: UpdateImpl]]


def delete(db: Session, id: UUID) -> bool:
    [[LLM_INJECTION_START: DeleteImpl]]
    rows = db.query({{EntityName}}).filter({{EntityName}}.id == id).delete()
    db.commit()
    return rows > 0
    [[LLM_INJECTION_END: DeleteImpl]]

from datetime import datetime
from uuid import UUID
from pydantic import BaseModel, ConfigDict


class {{EntityName}}Base(BaseModel):
    [[LLM_INJECTION_START: BaseFields]]
    # LLM fills: Pydantic field declarations for non-pk fields based on Schema.
    # Type-annotate everything (str, int, float, bool, datetime).
    [[LLM_INJECTION_END: BaseFields]]


class Create{{EntityName}}({{EntityName}}Base):
    pass


class {{EntityName}}({{EntityName}}Base):
    model_config = ConfigDict(from_attributes=True)

    id: UUID
    created_at: datetime
    updated_at: datetime

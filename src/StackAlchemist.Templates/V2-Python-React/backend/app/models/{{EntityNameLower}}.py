import uuid
from datetime import datetime
from sqlalchemy import Column, DateTime, String, Integer, Numeric, Boolean
from sqlalchemy.dialects.postgresql import UUID
from app.database import Base


class {{EntityName}}(Base):
    __tablename__ = "{{TableName}}"

    id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    [[LLM_INJECTION_START: ColumnDefinitions]]
    # LLM fills: Column() declarations for non-pk fields based on Schema.
    # Use Integer / Numeric / Boolean / DateTime / String / UUID as appropriate.
    [[LLM_INJECTION_END: ColumnDefinitions]]
    created_at = Column(DateTime, nullable=False, default=datetime.utcnow)
    updated_at = Column(DateTime, nullable=False, default=datetime.utcnow, onupdate=datetime.utcnow)

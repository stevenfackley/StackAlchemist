"""Initial schema: applies 001_initial_schema.sql, the SQL generated for your entities.

Revision ID: 0001
Revises:
"""
from pathlib import Path

from alembic import op

revision = "0001"
down_revision = None
branch_labels = None
depends_on = None

SCHEMA_SQL = Path(__file__).with_name("001_initial_schema.sql")


def upgrade() -> None:
    # Straight to the driver with no parameters: psycopg then sends the file with the simple
    # query protocol, which runs several statements at once and reads `%` literally.
    dbapi_connection = op.get_bind().connection.dbapi_connection
    with dbapi_connection.cursor() as cursor:
        cursor.execute(SCHEMA_SQL.read_text(encoding="utf-8"))


def downgrade() -> None:
    raise NotImplementedError("The initial schema has no automatic downgrade; drop its tables by hand.")

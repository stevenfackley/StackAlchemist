from uuid import UUID
from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.orm import Session
from app.database import get_db
from app.repositories import {{EntityNameLower}} as repo
from app.schemas.{{EntityNameLower}} import {{EntityName}}, Create{{EntityName}}

router = APIRouter(prefix="/api/{{EntityNameLower}}s", tags=["{{EntityNameLower}}s"])


@router.get("/", response_model=list[{{EntityName}}])
def list_{{EntityNameLower}}s(db: Session = Depends(get_db)):
    return repo.get_all(db)


@router.get("/{id}", response_model={{EntityName}})
def get_{{EntityNameLower}}(id: UUID, db: Session = Depends(get_db)):
    item = repo.get_by_id(db, id)
    if item is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND)
    return item


@router.post("/", response_model={{EntityName}}, status_code=status.HTTP_201_CREATED)
def create_{{EntityNameLower}}(payload: Create{{EntityName}}, db: Session = Depends(get_db)):
    return repo.create(db, payload)


@router.put("/{id}", response_model={{EntityName}})
def update_{{EntityNameLower}}(id: UUID, payload: Create{{EntityName}}, db: Session = Depends(get_db)):
    item = repo.update(db, id, payload)
    if item is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND)
    return item


@router.delete("/{id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_{{EntityNameLower}}(id: UUID, db: Session = Depends(get_db)):
    if not repo.delete(db, id):
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND)

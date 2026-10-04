{{#each Entities}}
from app.models.{{NameLower}} import {{Name}}  # noqa: F401
{{/each}}

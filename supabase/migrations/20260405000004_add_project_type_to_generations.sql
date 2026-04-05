alter table public.generations
  add column if not exists project_type text not null default 'DotNetNextJs';

alter table public.generations
  drop constraint if exists generations_project_type_check;

alter table public.generations
  add constraint generations_project_type_check
  check (project_type in ('DotNetNextJs', 'PythonReact'));

create index if not exists idx_generations_project_type
  on public.generations(project_type);

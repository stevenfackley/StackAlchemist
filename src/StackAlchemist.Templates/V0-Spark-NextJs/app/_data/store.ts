export type Todo = { id: string; title: string; done: boolean };

type Store = { todos: Todo[]; seq: number };

const globalForStore = globalThis as unknown as { __todoStore?: Store };

const store: Store =
  globalForStore.__todoStore ??
  (globalForStore.__todoStore = {
    todos: [
      { id: "1", title: "Edit app/page.tsx and watch it hot-reload", done: false },
      { id: "2", title: "Add a task using the box above", done: false },
    ],
    seq: 3,
  });

export function listTodos(): Todo[] {
  return store.todos;
}

export function addTodo(title: string): Todo {
  const todo: Todo = { id: String(store.seq++), title, done: false };
  store.todos.push(todo);
  return todo;
}

export function toggleTodo(id: string): Todo | null {
  const todo = store.todos.find((t) => t.id === id);
  if (!todo) return null;
  todo.done = !todo.done;
  return todo;
}

export function removeTodo(id: string): boolean {
  const before = store.todos.length;
  store.todos = store.todos.filter((t) => t.id !== id);
  return store.todos.length < before;
}

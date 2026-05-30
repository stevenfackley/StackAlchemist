"use client";

import { useEffect, useState } from "react";

type Todo = { id: string; title: string; done: boolean };

export default function HomePage() {
  const [todos, setTodos] = useState<Todo[]>([]);
  const [title, setTitle] = useState("");
  const [loading, setLoading] = useState(true);

  async function load() {
    const res = await fetch("/api/todos");
    const data = await res.json();
    setTodos(data.todos);
    setLoading(false);
  }

  useEffect(() => {
    load();
  }, []);

  async function addTodo(e: React.FormEvent) {
    e.preventDefault();
    const text = title.trim();
    if (!text) return;
    setTitle("");
    await fetch("/api/todos", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ title: text }),
    });
    await load();
  }

  async function toggle(id: string) {
    await fetch(`/api/todos/${id}`, { method: "PATCH" });
    await load();
  }

  async function remove(id: string) {
    await fetch(`/api/todos/${id}`, { method: "DELETE" });
    await load();
  }

  const remaining = todos.filter((t) => !t.done).length;

  return (
    <main className="wrap">
      <header className="head">
        <h1 className="title">{{ProjectName}}</h1>
        <p className="subtitle">A working demo — add, complete, and delete tasks.</p>
      </header>

      <form className="composer" onSubmit={addTodo}>
        <input
          className="input"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder="What needs doing?"
          aria-label="New task"
        />
        <button className="btn" type="submit">
          Add
        </button>
      </form>

      {loading ? (
        <p className="muted">Loading…</p>
      ) : todos.length === 0 ? (
        <p className="muted">No tasks yet. Add your first one above.</p>
      ) : (
        <ul className="list">
          {todos.map((t) => (
            <li className="row" key={t.id}>
              <label className="check">
                <input
                  type="checkbox"
                  checked={t.done}
                  onChange={() => toggle(t.id)}
                />
                <span className={t.done ? "done" : ""}>{t.title}</span>
              </label>
              <button
                className="link"
                type="button"
                onClick={() => remove(t.id)}
                aria-label="Delete task"
              >
                Delete
              </button>
            </li>
          ))}
        </ul>
      )}

      <footer className="foot">
        <span>{remaining} remaining</span>
      </footer>
    </main>
  );
}

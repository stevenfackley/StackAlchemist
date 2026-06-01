import { NextResponse } from "next/server";
import { listTodos, addTodo } from "../../_data/store";

export const dynamic = "force-dynamic";

export async function GET() {
  return NextResponse.json({ todos: listTodos() });
}

export async function POST(request: Request) {
  const body = await request.json().catch(() => ({}));
  const title = typeof body.title === "string" ? body.title.trim() : "";
  if (!title) {
    return NextResponse.json({ error: "title is required" }, { status: 400 });
  }
  const todo = addTodo(title);
  return NextResponse.json({ todo }, { status: 201 });
}

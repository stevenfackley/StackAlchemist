import { NextResponse } from "next/server";
import { toggleTodo, removeTodo } from "../../../_data/store";

export const dynamic = "force-dynamic";

export async function PATCH(
  _request: Request,
  context: { params: Promise<{ id: string }> }
) {
  const { id } = await context.params;
  const todo = toggleTodo(id);
  if (!todo) {
    return NextResponse.json({ error: "not found" }, { status: 404 });
  }
  return NextResponse.json({ todo });
}

export async function DELETE(
  _request: Request,
  context: { params: Promise<{ id: string }> }
) {
  const { id } = await context.params;
  const ok = removeTodo(id);
  if (!ok) {
    return NextResponse.json({ error: "not found" }, { status: 404 });
  }
  return NextResponse.json({ ok: true });
}

import { Suspense } from "react";
import { getGeneration } from "@/lib/actions";
import { GenerateClientPage } from "./GenerateClientPage";
import Link from "next/link";
import Image from "next/image";

interface PageProps {
  params: Promise<{ id: string }>;
}

export default async function GeneratePage({ params }: PageProps) {
  const { id } = await params;
  const generation = await getGeneration(id);

  if (!generation) {
    return (
      <div className="min-h-screen bg-slate-800 flex flex-col items-center justify-center px-4">
        <div className="text-center space-y-6 max-w-md">
          <div className="flex justify-center">
            <Link href="/">
              <Image src="/logo.svg" alt="Stack Alchemist" width={48} height={48} className="opacity-60" />
            </Link>
          </div>
          <h1 className="text-2xl font-bold text-white">Generation Not Found</h1>
          <p className="text-slate-400 text-sm leading-relaxed">
            The generation ID <code className="text-blue-400 font-mono text-xs bg-slate-700/50 px-1.5 py-0.5 rounded">{id}</code> does not exist or has expired.
          </p>
          <Link
            href="/"
            className="inline-block rounded-full bg-blue-500 text-white px-6 py-2.5 text-sm font-medium hover:bg-blue-400 transition-all duration-300"
          >
            Start a New Generation &rarr;
          </Link>
        </div>
      </div>
    );
  }

  return (
    <Suspense
      fallback={
        <div className="min-h-screen bg-slate-800 flex items-center justify-center">
          <div className="h-8 w-8 border-2 border-blue-500 border-t-transparent rounded-full animate-spin" />
        </div>
      }
    >
      <GenerateClientPage initialGeneration={generation} generationId={id} />
    </Suspense>
  );
}

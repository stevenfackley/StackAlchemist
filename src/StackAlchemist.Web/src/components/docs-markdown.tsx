"use client";

import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import rehypeHighlight from "rehype-highlight";
import type { Components } from "react-markdown";
import Link from "next/link";

const components: Components = {
  // Headings
  h1: ({ children }) => (
    <h1 className="text-2xl sm:text-3xl font-bold text-white mt-0 mb-6 font-sans tracking-tight leading-tight">
      {children}
    </h1>
  ),
  h2: ({ children }) => (
    <h2 className="text-xl font-bold text-white mt-10 mb-4 font-sans tracking-tight border-b border-slate-700/60 pb-2">
      {children}
    </h2>
  ),
  h3: ({ children }) => (
    <h3 className="text-base font-bold text-white mt-7 mb-3 font-sans">
      {children}
    </h3>
  ),
  h4: ({ children }) => (
    <h4 className="text-sm font-bold text-slate-200 mt-5 mb-2 font-sans uppercase tracking-wide">
      {children}
    </h4>
  ),

  // Paragraphs
  p: ({ children }) => (
    <p className="text-slate-300 text-sm leading-relaxed mb-4">{children}</p>
  ),

  // Links — internal /docs/* links use Next.js Link, external open new tab
  a: ({ href, children }) => {
    if (href?.startsWith("/") || href?.startsWith("./") || href?.startsWith("../")) {
      // Normalise relative doc links to absolute /docs/ paths
      const resolved = href.startsWith("./")
        ? `/docs/${href.slice(2)}`
        : href.startsWith("../user/")
        ? `/docs/${href.slice(8)}`
        : href.startsWith("../architecture/")
        ? href // leave architecture links as-is for now
        : href;
      return (
        <Link
          href={resolved}
          className="text-blue-400 hover:text-blue-300 underline underline-offset-2 transition-colors"
        >
          {children}
        </Link>
      );
    }
    return (
      <a
        href={href}
        target="_blank"
        rel="noopener noreferrer"
        className="text-blue-400 hover:text-blue-300 underline underline-offset-2 transition-colors"
      >
        {children}
      </a>
    );
  },

  // Inline code
  code: ({ className, children, ...props }) => {
    // Block code gets a className like language-xxx; inline code does not
    const isBlock = Boolean(className);
    if (isBlock) {
      return (
        <code className={`${className ?? ""} text-sm`} {...props}>
          {children}
        </code>
      );
    }
    return (
      <code className="bg-slate-700/60 text-blue-300 font-mono text-[0.8em] px-1.5 py-0.5 rounded">
        {children}
      </code>
    );
  },

  // Code blocks
  pre: ({ children }) => (
    <pre className="bg-slate-900 border border-slate-700/60 rounded-lg p-4 overflow-x-auto text-sm font-mono my-5 text-slate-200 leading-relaxed">
      {children}
    </pre>
  ),

  // Blockquote
  blockquote: ({ children }) => (
    <blockquote className="border-l-2 border-blue-400/50 pl-4 my-4 text-slate-400 italic">
      {children}
    </blockquote>
  ),

  // Lists
  ul: ({ children }) => (
    <ul className="list-disc list-outside pl-5 space-y-1.5 mb-4 text-slate-300 text-sm">
      {children}
    </ul>
  ),
  ol: ({ children }) => (
    <ol className="list-decimal list-outside pl-5 space-y-1.5 mb-4 text-slate-300 text-sm">
      {children}
    </ol>
  ),
  li: ({ children }) => <li className="leading-relaxed">{children}</li>,

  // Tables
  table: ({ children }) => (
    <div className="overflow-x-auto my-6">
      <table className="w-full text-sm border-collapse">{children}</table>
    </div>
  ),
  thead: ({ children }) => <thead className="bg-slate-800">{children}</thead>,
  tbody: ({ children }) => (
    <tbody className="divide-y divide-slate-700/50">{children}</tbody>
  ),
  th: ({ children }) => (
    <th className="text-left px-4 py-2.5 font-mono text-[10px] tracking-widest text-blue-400 uppercase border-b border-slate-700/60">
      {children}
    </th>
  ),
  td: ({ children }) => (
    <td className="px-4 py-2.5 text-slate-300 border-b border-slate-700/30">
      {children}
    </td>
  ),
  tr: ({ children }) => (
    <tr className="hover:bg-slate-700/20 transition-colors">{children}</tr>
  ),

  // Horizontal rule
  hr: () => <hr className="border-slate-700/60 my-8" />,

  // Strong / em
  strong: ({ children }) => <strong className="font-bold text-white">{children}</strong>,
  em: ({ children }) => <em className="italic text-slate-300">{children}</em>,
};

interface DocsMarkdownProps {
  content: string;
}

export function DocsMarkdown({ content }: DocsMarkdownProps) {
  return (
    <div className="docs-content">
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[rehypeHighlight]}
        components={components}
      >
        {content}
      </ReactMarkdown>
    </div>
  );
}

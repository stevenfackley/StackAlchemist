export function BackgroundEffects() {
  return (
    <div className="fixed inset-0 overflow-hidden pointer-events-none select-none" aria-hidden>
      <div
        className="absolute top-1/4 -right-1/4 h-[300px] w-[300px] md:h-[600px] md:w-[600px] lg:h-[800px] lg:w-[800px] rounded-full animate-pulse-glow"
        style={{
          backgroundImage: "radial-gradient(circle, rgba(59, 130, 246, 0.15) 0%, rgba(59, 130, 246, 0.05) 45%, transparent 70%)",
        }}
      />
      <div
        className="absolute -bottom-1/4 -left-1/4 h-[240px] w-[240px] md:h-[480px] md:w-[480px] lg:h-[600px] lg:w-[600px] rounded-full animate-pulse-glow"
        style={{
          backgroundImage: "radial-gradient(circle, rgba(99, 102, 241, 0.08) 0%, transparent 55%)",
          animationDelay: "2s",
        }}
      />
      <div
        className="absolute top-1/2 left-1/3 h-[240px] w-[360px] md:h-[320px] md:w-[480px] lg:h-[400px] lg:w-[600px] rounded-full opacity-40"
        style={{
          backgroundImage: "radial-gradient(ellipse, rgba(148, 163, 184, 0.06) 0%, transparent 60%)",
        }}
      />
      {/* Subtle grid */}
      <div
        className="absolute inset-0 opacity-[0.025]"
        style={{
          backgroundImage:
            "linear-gradient(rgba(255,255,255,0.3) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.3) 1px, transparent 1px)",
          backgroundSize: "80px 80px",
        }}
      />
    </div>
  );
}

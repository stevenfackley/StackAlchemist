"use client";

import { BackgroundEffects } from "@/components/home/background-effects";
import { HomeNav } from "@/components/home/home-nav";
import { HeroSection } from "@/components/home/hero-section";
import { LaunchConsole } from "@/components/home/launch-console";
import { ExampleAppsSection } from "@/components/home/example-apps-section";
import { FeaturesSection } from "@/components/home/features-section";
import { PricingSection } from "@/components/home/pricing-section";
import { FaqSection } from "@/components/home/faq-section";
import { CtaSection } from "@/components/home/cta-section";
import { HomeFooter } from "@/components/home/home-footer";

export default function HomePage() {
  return (
    <div className="min-h-screen flex flex-col bg-slate-800 relative overflow-x-hidden">
      <BackgroundEffects />
      <HomeNav />
      <main>
        <HeroSection />
        <LaunchConsole />
        <ExampleAppsSection />
        <FeaturesSection />
        <PricingSection />
        <FaqSection />
        <CtaSection />
      </main>
      <HomeFooter />
      <div className="fixed bottom-0 left-0 right-0 h-px bg-gradient-to-r from-transparent via-blue-500/30 to-transparent z-50" />
    </div>
  );
}

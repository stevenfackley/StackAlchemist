import {
  ShoppingCart,
  BarChart3,
  Heart,
  ClipboardList,
  Home,
  Utensils,
  BookOpen,
  Ticket,
  Briefcase,
  Package,
  Users,
  MessageSquare,
  Dumbbell,
  Calendar,
  CreditCard,
  Gamepad2,
  Truck,
  Hotel,
} from "lucide-react";

const EXAMPLE_APPS = [
  {
    icon: ShoppingCart,
    name: "E-commerce Platform",
    description: "Multi-vendor marketplace with inventory, orders, product catalog, and Stripe payments.",
    tags: ["Products", "Orders", "Vendors"],
  },
  {
    icon: BarChart3,
    name: "SaaS Analytics Dashboard",
    description: "Real-time KPI tracking, custom report builder, team workspaces, and alert pipelines.",
    tags: ["Metrics", "Reports", "Teams"],
  },
  {
    icon: Heart,
    name: "Healthcare Patient Portal",
    description: "Patient records, appointment scheduling, prescription management, and provider directory.",
    tags: ["Patients", "Appointments", "EHR"],
  },
  {
    icon: ClipboardList,
    name: "Project Management Tool",
    description: "Sprints, tasks, boards, time tracking, and team collaboration with Kanban views.",
    tags: ["Tasks", "Sprints", "Teams"],
  },
  {
    icon: Home,
    name: "Real Estate Platform",
    description: "Property listings, agent profiles, virtual tours, lead management, and MLS sync.",
    tags: ["Listings", "Agents", "Leads"],
  },
  {
    icon: Utensils,
    name: "Food Delivery App",
    description: "Restaurant catalog, menu management, order tracking, and driver dispatch system.",
    tags: ["Orders", "Drivers", "Menus"],
  },
  {
    icon: BookOpen,
    name: "Learning Management System",
    description: "Courses, video lessons, quizzes, certificates, progress tracking, and cohorts.",
    tags: ["Courses", "Quizzes", "Certs"],
  },
  {
    icon: Ticket,
    name: "Event Ticketing Platform",
    description: "Event creation, seat maps, QR ticket issuance, capacity management, and check-in.",
    tags: ["Events", "Tickets", "Check-in"],
  },
  {
    icon: Briefcase,
    name: "Freelancer Marketplace",
    description: "Talent profiles, project bids, contract management, escrow payments, and reviews.",
    tags: ["Profiles", "Bids", "Escrow"],
  },
  {
    icon: Package,
    name: "Inventory Management",
    description: "Stock levels, supplier management, purchase orders, warehouses, and reorder alerts.",
    tags: ["Stock", "Suppliers", "POs"],
  },
  {
    icon: Users,
    name: "HR Onboarding Tool",
    description: "Employee records, onboarding flows, document signing, org charts, and payroll sync.",
    tags: ["Employees", "Docs", "Payroll"],
  },
  {
    icon: MessageSquare,
    name: "Customer Support Platform",
    description: "Ticket routing, live chat, knowledge base, SLA tracking, and CSAT reporting.",
    tags: ["Tickets", "Chat", "SLA"],
  },
  {
    icon: Dumbbell,
    name: "Fitness & Wellness App",
    description: "Workout plans, nutrition logs, progress charts, coach assignments, and challenges.",
    tags: ["Workouts", "Nutrition", "Coach"],
  },
  {
    icon: Calendar,
    name: "Appointment Booking System",
    description: "Service catalog, availability calendar, automated reminders, and payment collection.",
    tags: ["Bookings", "Calendar", "Payments"],
  },
  {
    icon: CreditCard,
    name: "FinTech Dashboard",
    description: "Multi-account management, transaction history, budget categories, and investment tracking.",
    tags: ["Accounts", "Budgets", "Investments"],
  },
  {
    icon: Gamepad2,
    name: "Gaming Leaderboard",
    description: "Player profiles, match history, ELO ratings, achievement system, and tournament brackets.",
    tags: ["Players", "Matches", "Rankings"],
  },
  {
    icon: Truck,
    name: "Logistics Tracker",
    description: "Shipment management, route optimization, driver tracking, POD capture, and billing.",
    tags: ["Shipments", "Routes", "Billing"],
  },
  {
    icon: Hotel,
    name: "Hotel Management System",
    description: "Room inventory, reservations, housekeeping schedules, POS billing, and guest profiles.",
    tags: ["Rooms", "Reservations", "POS"],
  },
];

export function ExampleAppsSection() {
  return (
    <section className="relative z-10 border-t border-slate-600/30 py-24 px-6 sm:px-8 lg:px-16">
      <div className="mx-auto max-w-7xl">
        <div className="mb-14 text-center">
          <div className="mb-4 flex items-center justify-center gap-3">
            <div className="h-px w-12 bg-gradient-to-r from-transparent via-blue-500/60 to-transparent" />
            <span className="font-mono text-xs tracking-[0.3em] text-blue-400 uppercase">What you can build</span>
            <div className="h-px w-12 bg-gradient-to-r from-blue-500/60 via-transparent to-transparent" />
          </div>
          <h2 className="text-3xl font-bold text-white lg:text-4xl mb-4">
            One Platform. Infinite Applications.
          </h2>
          <p className="mx-auto max-w-2xl text-slate-400 leading-relaxed">
            StackAlchemist turns a paragraph into a production-ready codebase in under 90 seconds.
            Here&rsquo;s a taste of what you can synthesize today.
          </p>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {EXAMPLE_APPS.map((app) => {
            const Icon = app.icon;
            return (
              <div
                key={app.name}
                className="group rounded-xl border border-slate-600/30 bg-slate-700/20 p-5 transition-all duration-300 hover:border-blue-500/30 hover:bg-slate-700/40 hover:shadow-[0_0_20px_rgba(59,130,246,0.08)] cursor-default"
              >
                <div className="mb-3 flex items-center gap-3">
                  <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-blue-500/10 text-blue-400 transition-colors group-hover:bg-blue-500/20 shrink-0">
                    <Icon className="h-5 w-5" />
                  </div>
                  <h3 className="text-sm font-semibold text-white leading-tight">{app.name}</h3>
                </div>
                <p className="text-xs leading-relaxed text-slate-400 mb-3">{app.description}</p>
                <div className="flex flex-wrap gap-1.5">
                  {app.tags.map((tag) => (
                    <span
                      key={tag}
                      className="rounded-full border border-slate-600/30 bg-slate-800/50 px-2 py-0.5 text-[10px] font-mono text-slate-500"
                    >
                      {tag}
                    </span>
                  ))}
                </div>
              </div>
            );
          })}
        </div>

        <div className="mt-10 text-center">
          <p className="text-slate-400 text-sm mb-4">
            Don&rsquo;t see your use case? If you can describe it, StackAlchemist can synthesize it.
          </p>
          <button
            onClick={() => window.scrollTo({ top: 0, behavior: "smooth" })}
            className="rounded-full border border-blue-500/30 bg-blue-500/10 px-6 py-2.5 text-sm text-blue-400 font-medium transition-all duration-300 hover:bg-blue-500/20 hover:border-blue-500/50"
          >
            Start Synthesizing ↑
          </button>
        </div>
      </div>
    </section>
  );
}

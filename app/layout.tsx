import type { Metadata } from "next";
import "./globals.css";
export const metadata: Metadata = { title: "UPS Sunrise Position Checklists", description: "Choose your jambreaker position, complete its Sunrise checklist and download a PDF. Prototype for review.", icons: { icon: "/favicon.svg" } };
export default function RootLayout({children}: Readonly<{children:React.ReactNode}>) { return <html lang="en"><body>{children}</body></html>; }

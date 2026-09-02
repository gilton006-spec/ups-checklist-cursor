import type { Metadata } from "next";
import "./globals.css";
export const metadata: Metadata = { title: "UPS PD4 Checklist", description: "Tap to complete your Sunrise checklist and download a finished PDF.", icons: { icon: "/favicon.svg" } };
export default function RootLayout({children}: Readonly<{children:React.ReactNode}>) { return <html lang="en"><body>{children}</body></html>; }

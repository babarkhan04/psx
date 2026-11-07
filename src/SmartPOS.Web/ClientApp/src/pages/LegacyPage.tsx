import { ExternalLink } from 'lucide-react';

export default function LegacyPage({ title, href }: { title: string; href: string }) {
  return <div className="empty-state"><ExternalLink size={34}/><h2>{title}</h2><p>This module is still using the existing Blazor screen during the staged React conversion.</p><a className="primary-button link-button" href={href}>Open current module</a></div>;
}

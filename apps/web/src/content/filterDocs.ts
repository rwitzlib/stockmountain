/**
 * Filter DSL docs, sourced from the repo-level `docs/filters/*.md` at build time (Vite glob).
 * Those files are the spec each function is implemented against (plan 15); the API's
 * `/filters/functions` returns `docsUrl: /docs/filters/<name>` pointing here.
 */
const modules = import.meta.glob('../../../../docs/filters/*.md', {
  query: '?raw',
  import: 'default',
  eager: true,
}) as Record<string, string>;

export interface FilterDocFrontmatter {
  name?: string;
  title?: string;
  kind?: string;
  signature?: string;
  contexts?: string[];
  since?: string;
  summary?: string;
}

export interface FilterDoc {
  slug: string;
  frontmatter: FilterDocFrontmatter;
  body: string;
}

function parseFrontmatter(raw: string): { frontmatter: FilterDocFrontmatter; body: string } {
  const match = /^---\r?\n([\s\S]*?)\r?\n---\r?\n?/.exec(raw);
  if (!match) return { frontmatter: {}, body: raw };
  const frontmatter: Record<string, unknown> = {};
  for (const line of match[1].split(/\r?\n/)) {
    const idx = line.indexOf(':');
    if (idx <= 0) continue;
    const key = line.slice(0, idx).trim();
    const rawValue = line.slice(idx + 1).trim();
    if (rawValue.startsWith('[') && rawValue.endsWith(']')) {
      frontmatter[key] = rawValue
        .slice(1, -1)
        .split(',')
        .map((s) => s.trim())
        .filter(Boolean);
    } else {
      frontmatter[key] = rawValue.replace(/^["']|["']$/g, '');
    }
  }
  return { frontmatter: frontmatter as FilterDocFrontmatter, body: raw.slice(match[0].length) };
}

const docs: Record<string, FilterDoc> = {};
for (const [path, raw] of Object.entries(modules)) {
  const slug = path.split('/').pop()!.replace(/\.md$/, '');
  const { frontmatter, body } = parseFrontmatter(raw);
  docs[slug] = { slug, frontmatter, body };
}

export const KIND_ORDER = ['keyword', 'series', 'transform', 'boolean'] as const;

export function getFilterDoc(slug: string): FilterDoc | undefined {
  return docs[slug];
}

/** Every function/keyword page (excludes index), sorted by kind then name. */
export function listFilterDocs(): FilterDoc[] {
  return Object.values(docs)
    .filter((d) => d.slug !== 'index')
    .sort((a, b) => {
      const ka = KIND_ORDER.indexOf((a.frontmatter.kind ?? '') as (typeof KIND_ORDER)[number]);
      const kb = KIND_ORDER.indexOf((b.frontmatter.kind ?? '') as (typeof KIND_ORDER)[number]);
      if (ka !== kb) return ka - kb;
      return a.slug.localeCompare(b.slug);
    });
}

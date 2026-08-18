import { useMemo } from 'react';
import { Link, useParams } from 'react-router-dom';
import { marked } from 'marked';
import { ArrowLeft, BookOpen } from 'lucide-react';
import { getFilterDoc, listFilterDocs, KIND_ORDER, type FilterDoc } from '../../content/filterDocs';

marked.setOptions({ gfm: true, breaks: false });

const KIND_LABEL: Record<string, string> = {
  keyword: 'Data keywords',
  series: 'Indicators',
  transform: 'Transforms',
  boolean: 'Conditions',
};

function ContextBadges({ contexts }: { contexts?: string[] }) {
  if (!contexts?.length) return null;
  return (
    <span className="inline-flex flex-wrap gap-1">
      {contexts.map((c) => (
        <span
          key={c}
          className="rounded border border-border bg-muted/40 px-1.5 py-0.5 text-[10px] font-medium uppercase tracking-wide text-muted-foreground"
        >
          {c}
        </span>
      ))}
    </span>
  );
}

function Markdown({ source }: { source: string }) {
  const html = useMemo(() => marked.parse(source) as string, [source]);
  // Content is our own repo markdown (docs/filters/*.md), not user input.
  return <article className="docs-prose" dangerouslySetInnerHTML={{ __html: html }} />;
}

function DocsIndex() {
  const index = getFilterDoc('index');
  const docs = listFilterDocs();
  const grouped = KIND_ORDER.map((kind) => ({
    kind,
    docs: docs.filter((d) => d.frontmatter.kind === kind),
  })).filter((g) => g.docs.length > 0);

  return (
    <div className="mx-auto max-w-4xl px-6 py-8">
      <div className="mb-6 flex items-center gap-2 text-sm text-muted-foreground">
        <BookOpen className="h-4 w-4" />
        <span>Docs</span>
        <span>/</span>
        <span className="text-foreground">Filters</span>
      </div>
      {index ? <Markdown source={index.body} /> : <h1 className="text-2xl font-semibold">Filter reference</h1>}

      <h2 className="mt-10 text-lg font-semibold">All functions &amp; keywords</h2>
      {grouped.map(({ kind, docs: group }) => (
        <section key={kind} className="mt-6">
          <h3 className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
            {KIND_LABEL[kind] ?? kind}
          </h3>
          <ul className="divide-y divide-border rounded-lg border border-border">
            {group.map((d) => (
              <li key={d.slug} className="flex flex-col gap-1 px-4 py-3 sm:flex-row sm:items-center sm:gap-4">
                <Link to={`/docs/filters/${d.slug}`} className="font-mono text-sm text-primary hover:underline">
                  {d.frontmatter.signature ?? d.slug}
                </Link>
                <span className="flex-1 text-sm text-muted-foreground">{d.frontmatter.summary}</span>
                <ContextBadges contexts={d.frontmatter.contexts} />
              </li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}

function DocPage({ doc }: { doc: FilterDoc }) {
  const fm = doc.frontmatter;
  return (
    <div className="mx-auto max-w-4xl px-6 py-8">
      <div className="mb-6 flex items-center gap-2 text-sm text-muted-foreground">
        <Link to="/docs/filters" className="inline-flex items-center gap-1 hover:text-foreground">
          <ArrowLeft className="h-4 w-4" /> Filter reference
        </Link>
        <span>/</span>
        <span className="font-mono text-foreground">{doc.slug}</span>
      </div>
      <div className="mb-6 flex flex-wrap items-center gap-3 text-xs text-muted-foreground">
        {fm.kind && <span className="rounded bg-muted px-2 py-0.5 font-medium capitalize">{fm.kind}</span>}
        <ContextBadges contexts={fm.contexts} />
        {fm.since && <span>since {fm.since}</span>}
      </div>
      <Markdown source={doc.body} />
    </div>
  );
}

export function FilterDocsPage() {
  const { name } = useParams<{ name?: string }>();
  if (!name) return <DocsIndex />;
  const doc = getFilterDoc(name);
  if (!doc) {
    return (
      <div className="mx-auto max-w-4xl px-6 py-8">
        <h1 className="text-xl font-semibold">No docs for "{name}"</h1>
        <p className="mt-2 text-sm text-muted-foreground">
          <Link to="/docs/filters" className="text-primary hover:underline">
            Back to the filter reference
          </Link>
        </p>
      </div>
    );
  }
  return <DocPage doc={doc} />;
}

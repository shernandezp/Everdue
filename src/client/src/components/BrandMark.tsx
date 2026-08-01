import { useId } from 'react';

/**
 * The application mark, drawn inline so it inherits nothing and needs no network request.
 *
 * Same geometry as public/favicon.svg and scripts/gen-icons.mjs — an open cycle (the recurring
 * obligation), a marker in the gap where the next period falls due, and the check that closes it.
 * Change the three together.
 */
export function BrandMark({ size = 28 }: { size?: number }) {
  // Two marks on one page (header and, on a narrow screen, the navbar) must not share a gradient id.
  const gradient = useId();

  return (
    <svg width={size} height={size} viewBox="0 0 32 32" role="img" aria-label="Everdue" style={{ flexShrink: 0 }}>
      <defs>
        <linearGradient id={gradient} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#4c6ef5" />
          <stop offset="1" stopColor="#0ca678" />
        </linearGradient>
      </defs>
      <rect width="32" height="32" rx="7.5" fill={`url(#${gradient})`} />
      <path
        d="M26.05 14.23A10.2 10.2 0 1 1 19.49 6.42"
        fill="none"
        stroke="#fff"
        strokeWidth="3.1"
        strokeLinecap="round"
      />
      <circle cx="23.81" cy="9.44" r="2" fill="#ffd43b" />
      <path
        d="M12 16.9l2.9 2.9L20.4 12.9"
        fill="none"
        stroke="#fff"
        strokeWidth="2.9"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

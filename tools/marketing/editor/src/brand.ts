// brand.json, typed: names, colours and fonts for everything the editor draws.
import brandJson from '../../brand.json';
import type { Brand } from './lib/types.ts';

export const brand = brandJson as unknown as Brand;

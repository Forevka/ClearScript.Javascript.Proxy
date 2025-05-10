import resolve from '@rollup/plugin-node-resolve';
import { terser } from 'rollup-plugin-terser';

export default {
  input: './dist/Jexl.js',      // your library’s entry point
  output: {
    file: 'minified/jexl.min.js',
    format: 'cjs',           
    name: 'Jexl',          
    sourcemap: true
  },
  plugins: [
    resolve(),               // so Rollup can find node_modules imports
    terser()                 // minify the bundle
  ]
};
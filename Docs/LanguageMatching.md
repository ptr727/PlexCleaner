# IETF Language Matching

Language tag matching supports [IETF / RFC 5646 / BCP 47](https://en.wikipedia.org/wiki/IETF_language_tag) tag formats as implemented by [MkvMerge](https://codeberg.org/mbunkus/mkvtoolnix/wiki/Languages-in-Matroska-and-MKVToolNix).

## Understanding Language Matching

Tags are in the form of `language-extlang-script-region-variant-extension-privateuse`, and matching happens left to right (most specific to least specific).

Examples:

- `pt` matches: `pt` Portuguese, `pt-BR` Brazilian Portuguese, `pt-PT` European Portuguese.
- `pt-BR` matches: only `pt-BR` Brazilian Portuguese.
- `zh` matches: `zh` Chinese, `zh-Hans` simplified Chinese, `zh-Hant` traditional Chinese, and other variants.
- `zh-Hans` matches: only `zh-Hans` simplified Chinese.

## Technical Details

During processing the absence of IETF language tags will be treated as a track warning, and an RFC 5646 IETF language will be temporarily assigned based on the ISO639-2B tag.\
If `ProcessOptions.SetIetfLanguageTags` is enabled MkvMerge will be used to remux the file using the `--normalize-language-ietf extlang` option, see the [MkvMerge documentation](https://mkvtoolnix.download/doc/mkvmerge.html) for more details.

Normalized tags will be expanded for matching.\
E.g. `cmn-Hant` will be expanded to `zh-cmn-Hant` allowing matching with `zh`.

## References

- See the [W3C Language tags in HTML and XML](https://www.w3.org/International/articles/language-tags/) and [BCP47 language subtag lookup](https://r12a.github.io/app-subtags/) for technical details.
- Language tag matching is implemented using the [LanguageTags](https://github.com/ptr727/LanguageTags) library.

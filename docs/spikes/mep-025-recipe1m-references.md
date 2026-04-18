# MEP-025 — Recipe1M+ References

Frozen reference material captured during the MEP-025 spike. Lifted verbatim from the MIT CSAIL project page on 2026-04-18 so the exact author list, paper titles, and BibTeX entries are pinned to the repo in case the upstream page reorganizes.

## Discovery path (each user follows this themselves)

1. **Project page** — https://im2recipe.csail.mit.edu/
   - Authoritative citation block (reproduced below)
   - Links to the Torralba Lab GitHub repo
2. **Code repository** — https://github.com/torralba-lab/im2recipe
   - Dev-friendly entry; documents the dataset request flow
3. **MIT signup + terms of use** — reached via the MIT page, gated behind account creation
   - Each user signs the agreement and downloads their own copy
   - This repository does NOT link directly to the gated download URLs, per MIT's terms

## Citations to include in `CITATION.cff`

Two papers should be cited when using Recipe1M+. Both BibTeX entries below are reproduced verbatim from the MIT page.

### TPAMI 2019 journal paper (the Recipe1M+ extension)

```bibtex
@article{marin2019learning,
  title = {Recipe1M+: A Dataset for Learning Cross-Modal Embeddings for Cooking Recipes and Food Images},
  author = {Marin, Javier and Biswas, Aritro and Ofli, Ferda and Hynes, Nicholas and
  Salvador, Amaia and Aytar, Yusuf and Weber, Ingmar and Torralba, Antonio},
  journal = {{IEEE} Trans. Pattern Anal. Mach. Intell.},
  year = {2019}
}
```

### CVPR 2017 conference paper (the original Recipe1M release)

```bibtex
@inproceedings{salvador2017learning,
  title={Learning Cross-modal Embeddings for Cooking Recipes and Food Images},
  author={Salvador, Amaia and Hynes, Nicholas and Aytar, Yusuf and Marin, Javier and
          Ofli, Ferda and Weber, Ingmar and Torralba, Antonio},
  booktitle={Proceedings of the IEEE Conference on Computer Vision and Pattern Recognition},
  year={2017}
}
```

## Notes

- No DOIs are listed on the MIT page. If a `CITATION.cff` requires them, look them up separately via IEEE Xplore or arXiv (the TPAMI paper is indexed at `10.1109/TPAMI.2019.2927476`; the CVPR paper has an arXiv preprint at `1810.06553` for the extended version).
- The TPAMI paper is "the most recent" per the MIT page. When the implementation ticket lands, cite both.
- Author ordering differs between the two papers — preserve it exactly as shown, since author order is meaningful in academic citation.

## Related

- Backlog item: [MEP-025](../backlog.md) — spike evaluating expanded recipe data sources
- Implementation follow-up: TBD (would live as MEP-026 once the spike recommends Recipe1M+ or an alternative)

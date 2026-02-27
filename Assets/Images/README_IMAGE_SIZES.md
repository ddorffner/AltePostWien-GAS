## ASPECT RATIOS
-DGN: ---
-ENT: ---
-GAS: ---
-HOF: 2:1


## DEPTH INPUT
depth_input images should be as big as possible for first step with high details.
- flux limits: longest side should not be over 1536px
- rough guidelines are therefore:
	- square: 1536 x 1536
	- pano: 1536x768

-DGN: 3512 x 1024 > make smaller
-ENT: 1024 x 1064 > make bigger
-GAS: 8440 x 4000 > make smaller
-HOF: 1600 x 800	> make smaller


## DEPTH OUTOUT FOR BLENDING
depth_output should not extend 4096px for vvvv? TBD

-DGN 6860 x 2000 > make smaller
-ENT ---
-GAS ---
-HOF ---

## IMAGE OUTOUT
image_output size should be a bit higher than actual projection.
will be determined on-site.
factors include: wrapping on model, projector distance.

perliminary sizes:
- DGN 13720 x 4000
- ENT
- GAS
- HOF

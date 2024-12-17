# WeightedCannyEdgeDetector

An implementation of a Canny Edge Detector modified for use in glacier borehole images, and developed as part fo a PhD, The Automated Analysis of Glacier Borehole images. The algorithm is discussed in the paper [Borehole and Ice Feature Annotation Tool (BIFAT): A program for the automatic and manual annotation of glacier borehole images](https://www.sciencedirect.com/science/article/abs/pii/S0098300412003111) published in Computers and Geosciences February 2013.

The modification largely involves being able to wrap the edges horizontally since a borehole image shows a full 360 degree image, and being able to specify horizontal and vertical weightings independently. The different weightings are largely because the sinusoidal edges in borehole images of ice are largely stronger vertically than horizontally. 

Note: The algroithm has been ripped from a project carried out in 2013. It's by no means well written or efficient. Time permitting, the plan is to make it both at some point.



 

"""
	Input file format:
     0    1 2  3  4  5  6  7      8       9    10    11     12  13
	time gx gy gz ax ay az dForce pForce dFlex pFlex speed lat long
"""

import random
file = open("data.txt", 'w')
time = 0
gx = 0
gy = 0
gz = 0
ax = 0
ay = 0
az = 0
dForce = 0
pForce = 0
dFlex = 0
pFlex = 0
pressure = 0
speed = 0
lat = 0
long = 0
random.seed(None)

for i in range(0, 200):
	file.write(
	str(time) + " " + 
	str(gx) + " " + 
	str(gy) + " " + 
	str(gz) + " " + 
	str(ax) + " " + 
	str(ay) + " " + 
	str(az) + " " + 
	str(dForce) + " " +
	str(pForce + 10) + " " +
	str(dFlex) + " " +
	str(pFlex + 10) + " " +
	str(pressure) + " " +
	str(speed) + " " + 
	str(lat) + " " +
	str(long) + 
	'\n')
	time += .2
	speed = random.randint(40, 100)
	gx = random.randrange(0, 100) / float(100)
	gy = random.randrange(0, 100) / float(100)
	gz = random.randrange(0, 100) / float(100)
	ax = random.randrange(0, 100) / float(100)
	ay = random.randrange(0, 100) / float(100)
	az = random.randrange(0, 100) / float(100)
	dForce = random.randint(0, 60)
	pForce = dForce
	dFlex = random.randint(0, 100)
	pFlex = dFlex
	pressure = random.randint(60, 100)
	lat = random.randint(100, 300)
	long = random.randint(100, 300)
	
file.close
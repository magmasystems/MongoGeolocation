# Mongo Geolocation

## Getting the sample data

* Go to the site <https://catalog.data.gov/dataset/hospital-general-information-1da2c>

* Click on the button that lets you download the CSV file
  * <https://data.medicare.gov/api/views/xubh-q36u/rows.csv?accessType=DOWNLOAD>

* Use Excel or a CSV editor to delete all of the columns that are to the right of the `Location` column. The columns that remain should be:
  * Facility ID
  * Facility Name
  * Address
  * City
  * State
  * ZIP Code
  * Location

* We now want to import the hospital data into Mongo.
  * copy the file to the directory where mongoimport is found. This is usually `/usr/local/bin`
  * Run the command
  
``` shell
   mongoimport --host=127.0.0.1:27017 --db=GeoTest --collection=hospitals --file=hospitals.csv --type=csv --headerline
```

* You can use a Mongo IDE like Robo3T to explore the data. For example, we can find all of the hospitals in Brooklyn, New York.

``` mongo
db.getCollection('hospitals').find(
{
   $and: [
    { 'State': 'NY' },
    { 'ZIP Code': { '$gte': 11200, '$lt': 11300 } }
   ]
},
{
   'Facility Name': 1, 'Address': 1, 'City': 1, 'State': 1, 'ZIP Code': 1
})
.sort( { 'ZIP Code': 1 } )
```

* The Location column has an optional geolocation, but it can be blank or inaccurate. We want to use a mapping service (like Mapbox) to retrieve the geocoordinates from the street address. We want to add that retrieved geolocation data to a new field in the Hospital class called `Point`.

---

## Subscribe to a mapping service

* I went to the Mapbox website and registered. I created a new API key. This API key should be inserted into the `appsettings.json` file.

---

## Add the Geocoordinates to the Mongo Collection

* Compile the program. Run the command:

``` shell
dotnet MongoGeolocation update
```

* If you look at the Mongo database using a tool like Robo3T, you will see that a new field named `Point` was added to each of the 5300 hospital records.

* We should add an index on the new `Point` field. This will help with the `$nearSphere` searches.
  
```mongo
db.hospitals.createIndex({ Point: "2dsphere" })
```

---

## Run the Program

* The app will find all hospitals in Brooklyn, New York. It will print out each hospital, along with a list of hospitals that are 3 miles away from that hospital.

``` shell
dotnet MongoGeolocation
```

The `appsettings.json` file controls some of the parameters of the program.

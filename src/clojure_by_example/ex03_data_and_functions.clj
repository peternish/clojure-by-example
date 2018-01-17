(ns clojure-by-example.ex03-data-and-functions)


;; Ex03: LESSON GOALS (build upon ex01, and ex02)
;;
;; - Learn a few more nifty functions on collections (cousins of `map`)
;; - Learn to combine simple functions into more powerful functions
;; - Get a sense of how to model with data, and compute with functions
;; - Get a feel for some of the flexibility of Clojure data structures


;; Let's begin again, with our planets, our moons checker, and `map`
(def planets [{:name "Mercury" :moons 0  :mass 0.0533}
              {:name "Venus"   :moons 0  :mass 0.815}
              {:name "Earth"   :moons 1  :mass 1}
              {:name "Mars"    :moons 2  :mass 0.107}
              {:name "Jupiter" :moons 69 :mass 317.8}
              {:name "Saturn"  :moons 62 :mass 95.2}])


;; Recall: Our little "helper" function, to check if a planet has moons.
(defn planet-with-moons?
  "Given a 'planet' (assume it's a hash-map), return true if
  it has at least one moon."
  [planet]
  (> (:moons planet)
     0))

;; The following results make some sense:

(map :name planets) ; ok, we can now print just the names of planets

(map :moons planets) ; ok, we can now count total number of moons


;; But what do we do with a bunch of boolean results?

(map planet-with-moons? planets)


;; More usefully, I would like to know _which_ planets have moons.
;;
;; Even more usefully, given a collection of planets, I want to
;; filter all planets with moons.


;; EXERCISE:
;;
;; Predict the result of this expression:

(filter planet-with-moons?
        planets)


;; Can we do the opposite?

(filter (fn [p] (not (planet-with-moons? p)))
        planets)


;; EXERCISE
;;
;; You can define a new "helper" function, in terms of earlier helper
;; functions... Fix the function body, and make it work.

(defn planet-without-moons?
  "Planets without moons are exactly opposite to planets with moons."
  [FIX]
  FIX)


;; EXERCISE
;;
;; Now, filter out planets without moons...

;; (FIX FIX FIX) ; fixme and evaluate



;; Can we write a function to filter planets into two groups?
;; - planets with moons, and
;; - planets without moons?

;; EXERCISE:
;;
;; Will this work? Why?
;; - Take a guess... and then evaluate to check.
;;
{:planets-with-moons    (filter planet-with-moons? planets)
 :planets-without-moons (filter planet-without-moons? planets)}


;; Note: Do More With Less
;;
;; - Clojure lets us simply write down the structure of hash-maps,
;;   even if some values need to be computed. No special constructor
;;   required.
;;
;; - If an s-expression like (filter ...) is associated with a key,
;;   you may imagine that Clojure will replace the s-expression
;;   with the result of evaluating that expression, if we try to
;;   evaluate the whole hash-map at one go.
;;
;; By the way it's not just hash-maps, this works for vectors too,
;; for the same reason:

[(filter planet-with-moons?     planets)
 (filter planet-without-moons?  planets)]


;; EXERCISE:
;;
;; So now, to group planets by moons, we can... do what?

(defn group-planets-by-moons
  [planets]
  ;; FIX: return a data structure here that represents the grouping.
  'FIX)


;; EXERCISE:
;;
;; Use `group-planets-by-moons` to group planets.
;; Write your solution below:

#_('FIX 'FIX)

;; If you did it right, this is what happened:
;;
;; - When we called the function with `planets`, it did this:
;;   - evaluated each filter expression one by one
;;   - put the results in the respective places in the hash-map
;;   - returned the whole hash-map


;; Now we can further find...

(:planets-with-moons (group-planets-by-moons planets))



;; EXERCISE:
;;
;; Find names of those planets that have moons...

#_('FIX 'FIX 'FIX)


;; So far, we did some pretty cool things with sequence operations like
;; `map` and `filter`. Now for the big boss of sequence operations...



;; REDUCE!

;; Remember we can collect moons from planets?

(map :moons planets)


;; Now, how to "reduce" the collection into one number?
;; i.e., how to count the sum total of moons of all given planets?

;; Well...

(reduce + 0 (map :moons planets))
;; It computes this: 0 + p1-moons + p2-moons + .... + pN-moons

;; `reduce` takes a function, an "accumulator" value, and an input
;; collection, and returns an "accumulated" value.

;; Imagine each step of the above computation, like this:

;; =======================================
;; Accumulator | Input collection (of number of moons)
;; =======================================
;; 0 (start)   | [0 0 1 2]   ; just before first step
;; 0           | [0 1 2]     ; at end of first step
;; 0           | [1 2]
;; 1           | [2]
;; 3           | []          ; reduce detects empty collection
;; ---------------------------------------
;; 3 (return value)          ; reduce spits out the accumulator

;; In words...
;;
;; - To begin with, `reduce` passes the accumulator and the first item
;;   from the input collection to the function.
;;
;; - `reduce` "manages" the accumulator for us, such that the function
;;   can update the accumulator, and `reduce` will feed the updated
;;   accumulator back into the function, along with the next item
;;   in the input collection.
;;
;; - This continues until the input collection is exhausted.

;; `reductions` is a convenience function that helps us visualize
;; the "accumulator" at each step of the `reduce` computation:

(reductions + 0 [0 0 1 2]) ; compare with the table

(reductions + 0 (map :moons planets)) ; calculate total number of moons

(reductions + 2 (map :moons planets)) ; accumulator can be any number


;; Lesson-end Exercises:

;; IMPORTANT:
;;
;; - Don't refer back to the code above unless you're _really_ stuck.
;;
;; - Try to reason from first principles - use the basic ideas we have
;;   acquired so far.
;;
;; - Write your own functions _from scratch_, if you need them.


;; EXERCISE:
;;
;; Calculate the total mass of all `planets`
;; - Hint: this will be a one-liner s-expression
;; - Write your solution below:


;; EXERCISE:
;;
;; Count the number of `planets` that have moons.
;; - Reuse the `planet-with-moons?` function that we already defined.
;; - Use the `count` function to find the counts.

(count [42 43 44 45]) ; try this example

;; Write your solution below:



;; EXERCISE:
;;
;; Calculate the total mass of planets having moons.
;; - Reuse the `planet-with-moons?` function that we already defined.



;; EXERCISE:
;;
;; Calculate the total mass of planets _without_ moons.
;; - Reuse the `planet-without-moons?` function that we defined earlier.



;; EXERCISE:
;;
;; Write a function `massier-than-earth?` which,
;; - given a planet,
;; - returns true if the planet's mass exceeds Earth's mass
;; - returns false otherwise



;; EXERCISE:
;;
;; Write a function `planetary-stats` that:
;;
;; Takes TWO arguments:
;; 1. a "predicate" function like `massier-than-earth?`
;; 2. a sequence of some planets,
;;
;; Returns the following stats about those input planets
;; that match the filter criteria:
;; - count of the planets
;; - names of the planets
;; - total mass of the planets
;;
;; Important:
;; - The return value must be, an easy-to-query data structure.

;; Uncomment and fix:
#_(defn planetary-stats
    [pred-fn given-planets]
    ;; `let` is a way to define ("bind") function-local names to values
    (let [filtered-planets 'FIX]
      {:count 'FIX
       :names  'FIX
       :total-mass  'FIX}))



;; EXERCISE:
;;
;; Calculate `planetary-stats` for:
;;
;; - planets with moons:


;; EXERCISE:
;;
;; Calculate `planetary-stats` for:
;;
;; - planets without moons:



;; - planets without moons, using `complement`:
;;   compare, understand, use:

(planet-with-moons?              {:name "Earth" :moons 1})

((complement planet-with-moons?) {:name "Earth" :moons 1})

;; Fix the expression below:

#_(planetary-stats
   'FIX
   planets)


;; EXERCISE:
;;
;; Calculate `planetary-stats' for:
;;
;; - planets with more mass than the earth:



;; - planets with less mass than the earth, using `comp`:
;;   compare, understand, use:
#_(massier-than-earth?            {:name "Jupiter" :mass 317.8})

#_((comp not massier-than-earth?) {:name "Jupiter" :mass 317.8})

;; Type your solution here:



;; EXERCISE:
;;
;; Calculate `planetary-stats' for:
;;
;; - all `planets` (hint: use `identity`):



;; EXERCISE:
;;
;; Write a function `more-planetary-stats` that:
;; - Takes a sequence of planets, and
;; - Returns an easy-to-query data structure containing
;;   the following stats:
;;
;; given planets
;;     count
;;     names
;;     total mass
;; planets with moons
;;     count
;;     names
;;     total mass
;; planets without moons
;;     count
;;     names
;;     total mass
;; planets with more mass than earth
;;     count
;;     names
;;     total mass
;; planets having less mass than earth
;;     count
;;     names
;;     total mass


;; Fix the `more-planetary-stats` function below:

(defn more-planetary-stats
  [FIX]
  {:given-planets FIX
   :with-moons FIX
   :without-moons FIX
   :massier-than-earth FIX
   :less-massy-than-earth FIX})



;; Check your results. Uncomment and evaluate:
(comment
  (more-planetary-stats planets)

  (more-planetary-stats (take 2 planets))

  (more-planetary-stats (drop 2 planets)))



;; RECAP:
;;
;; - `map`, `filter`, and `reduce` are powerful sequence-processing
;;   functions. Clojure programmers use these heavily.
;;
;; - Clojure programmers define small functions that do one task well,
;;   and then build up sophisticated solutions by combining many such
;;   small functions.
;;
;; - We can directly write down the actual structure of a data structure,
;;   and Clojure will evaluate any un-evaluated values at run-time,
;;   and return us the same data structure, but with computed values.
;;
;; - Clojure data structures are very flexible, and allow us to
;;   model almost any real-world object. Previously we put hash-maps
;;   inside a sequence to represent a collection of planets. And, in
;;   this exercise, we put sequences inside a hash-map to group
;;   planets by moons.

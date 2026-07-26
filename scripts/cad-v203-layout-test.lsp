(vl-load-com)

(setq *v203-select-corner-1* '(-148.4651 276.5888 0.0))
(setq *v203-select-corner-2* '(408.9861 -131.0001 0.0))
(setq *v203-datum-hole-center* '(245.5 15.0 0.0))
(setq *v203-datum-corner* '(305.0 0.0 0.0))
(setq *v203-trace-file* nil)
(setq *v203-report-file* nil)

;; Freshness gate: when *v203-report-file* points at diagnostics/last-run.json,
;; COMPLETE is only written if the report file actually changed during this run.
(defun v203-report-systime ()
  (if (and *v203-report-file* (findfile *v203-report-file*))
    (vl-file-systime *v203-report-file*)
    nil
  )
)

(defun v203-trace (message / stream)
  (if *v203-trace-file*
    (progn
      (setq stream (open *v203-trace-file* "a"))
      (if stream
        (progn
          (write-line message stream)
          (close stream)
        )
      )
    )
  )
)

(defun v203-find-datum-circle (selection / index entity data center result)
  (setq index 0)
  (setq result nil)
  (while (and (< index (sslength selection)) (null result))
    (setq entity (ssname selection index))
    (setq data (entget entity))
    (if (= (cdr (assoc 0 data)) "CIRCLE")
      (progn
        (setq center (cdr (assoc 10 data)))
        (if (equal center *v203-datum-hole-center* 0.001)
          (setq result entity)
        )
      )
    )
    (setq index (1+ index))
  )
  result
)

(defun v203-side-keyword (side)
  (cond
    ((= side "Top") "T")
    ((= side "Bottom") "B")
    ((= side "Left") "L")
    ((= side "Right") "R")
    (T "A")
  )
)

(defun v203-run-side (side / old-cmdecho old-osmode selection datum-circle report-before)
  (setq old-cmdecho (getvar "CMDECHO"))
  (setq old-osmode (getvar "OSMODE"))
  (setvar "CMDECHO" 1)
  (setvar "OSMODE" 0)
  (v203-trace (strcat "START side=" side))
  (vl-cmdf "_.ZOOM" "_W" *v203-select-corner-1* *v203-select-corner-2*)
  (setq selection (ssget "_C" *v203-select-corner-1* *v203-select-corner-2*))
  (if selection
    (progn
      (setq datum-circle (v203-find-datum-circle selection))
      (if datum-circle
        (progn
          (v203-trace
            (strcat
              "INPUT selection="
              (itoa (sslength selection))
              " datumHandle="
              (cdr (assoc 5 (entget datum-circle)))
            )
          )
          (setq report-before (v203-report-systime))
          (vl-cmdf
            "ASD4"
            (v203-side-keyword side)
            selection
            ""
            datum-circle
            *v203-datum-corner*
            ""
            *v203-datum-corner*
            ""
          )
          (if (or (null *v203-report-file*)
                  (and (v203-report-systime)
                       (not (equal (v203-report-systime) report-before))))
            (v203-trace (strcat "COMPLETE side=" side))
            (v203-trace (strcat "ERROR stale-report side=" side))
          )
        )
        (v203-trace "ERROR datum circle not found")
      )
    )
    (v203-trace "ERROR selection is empty")
  )
  (setvar "OSMODE" old-osmode)
  (setvar "CMDECHO" old-cmdecho)
  (princ)
)

(defun c:V203TOP () (v203-run-side "Top"))
(defun c:V203BOTTOM () (v203-run-side "Bottom"))
(defun c:V203LEFT () (v203-run-side "Left"))
(defun c:V203RIGHT () (v203-run-side "Right"))

(princ "\nV203 layout test commands loaded: V203TOP/V203BOTTOM/V203LEFT/V203RIGHT")
(princ)

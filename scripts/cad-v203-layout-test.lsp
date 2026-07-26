(vl-load-com)

(setq *v203-select-corner-1* '(-148.4651 276.5888 0.0))
(setq *v203-select-corner-2* '(408.9861 -131.0001 0.0))
(setq *v203-datum-hole-center* '(245.5 15.0 0.0))
(setq *v203-datum-corner* '(305.0 0.0 0.0))
(setq *v203-trace-file* nil)
(setq *v203-report-file* nil)

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

(defun v203-file-stamp (path)
  (if (and path (findfile path))
    (vl-file-systime path)
    nil
  )
)

(defun v203-run-side (side / old-cmdecho old-osmode selection datum-circle report-stamp-before report-stamp-after)
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
          (setq report-stamp-before (v203-file-stamp *v203-report-file*))
          (v203-trace
            (strcat
              "INPUT selection="
              (itoa (sslength selection))
              " datumHandle="
              (cdr (assoc 5 (entget datum-circle)))
            )
          )
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
          (setq report-stamp-after (v203-file-stamp *v203-report-file*))
          (cond
            ((null *v203-report-file*)
              (v203-trace "ERROR diagnostic report path is not configured"))
            ((null report-stamp-after)
              (v203-trace "ERROR diagnostic report was not created"))
            ((equal report-stamp-before report-stamp-after)
              (v203-trace "ERROR diagnostic report was not refreshed"))
            (T
              (v203-trace (strcat "COMPLETE side=" side)))
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
